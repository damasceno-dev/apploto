using System.Globalization;
using System.Text.Json;
using FluentValidation;
using server.Application.Services.Receivables;
using server.Application.Services.Security;
using server.Application.Services.Transactions;
using server.Application.UseCases.GoLive.Openings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Imports;

public class GoLiveImportUseCase(
    IAuthenticationService authentication,
    IBranchUsersRepository branchUsers,
    BankCommandRunner runner,
    IGoLiveImportsRepository imports,
    RecordGoLiveOpeningUseCase openings,
    IAccountsRepository accounts,
    IClientsRepository clients,
    IOperatorsRepository operators,
    ITransactionTypesRepository types,
    IProductsRepository products,
    ITerminalsRepository terminals,
    GoLiveHistoryStage history,
    GoLivePeopleStage people,
    ISettingsRepository settings,
    IBranchClock clock)
{
    public const string ToolVersion = "m10-import-1";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<Guid, (string Evidence, AccessSource Source)> sourceCache = new();

    private async Task<BranchUser> Actor()
    {
        var actor = await authentication.GetAuthenticatedBranchUser();
        actor = await SecurityRules.FreshActor(branchUsers, actor);
        if (actor.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        return actor;
    }

    public async Task<IReadOnlyList<ResponseGoLiveImportJson>> List(CancellationToken ct)
    {
        var actor = await Actor();
        var result = new List<ResponseGoLiveImportJson>();
        foreach (var row in await imports.List(actor.BranchId, ct)) result.Add(await Response(row, false));
        return result;
    }

    public async Task<ResponseGoLiveImportJson> Get(Guid id, CancellationToken ct)
    {
        var actor = await Actor();
        return await Response(await Load(actor.BranchId, id, ct));
    }

    private async Task<GoLiveImport> Load(Guid branchId, Guid id, CancellationToken ct, bool tracking = false) =>
        await (tracking ? imports.GetForUpdate(branchId, id, ct) : imports.Get(branchId, id, ct)) ?? throw new NotFoundException(ResourcesErrorMessages.GOLIVE_IMPORT_NOT_FOUND);

    public async Task<ResponseGoLiveImportJson> Upload(RequestGoLiveImportJson request, string? key, CancellationToken ct)
    {
        _ = await Actor(); // Reject unauthorized callers before parsing private payloads.
        var sourceDigest = AccessSourceReader.EvidenceDigest(request);
        // Store raw source evidence (including original bytes), not a lossy normalized export.
        return await runner.Run<RequestGoLiveImportJson, ResponseGoLiveImportJson>("POST /go-live/imports", request, key,
            (actor, token) => Task.FromResult<IReadOnlyCollection<Guid>>([actor.BranchId]),
            async (actor, token) =>
            {
                // Byte identity locates a candidate; profile/encoding compatibility is a separate precondition.
                var prior = await imports.FindDigest(actor.BranchId, sourceDigest, token);
                if (prior is not null)
                {
                    EnsureSameInterpretation(JsonSerializer.Deserialize<RequestGoLiveImportJson>(prior.SourceJson, Json)!, request);
                    return (prior.Id, await Response(prior));
                }
                var source = AccessSourceReader.Read(request, sourceDigest);
                var row = new GoLiveImport
                {
                    BranchId = actor.BranchId,
                    UploadedByUserId = actor.UserId,
                    UpdatedByUserId = actor.UserId,
                    Profile = request.Profile,
                    SourceDigest = source.Digest,
                    SourceJson = JsonSerializer.Serialize(request, Json)
                };
                sourceCache[row.Id] = (row.SourceJson, source);
                await imports.Add(row, token);
                await Audit(row, actor.UserId, "Upload", row.SourceDigest, token);
                return (row.Id, await Response(row));
            }, ct, coordinatePeople: true);
    }

    public Task<ResponseGoLiveImportJson> Map(Guid id, RequestGoLiveMappingJson request, string? key, CancellationToken ct)
    {
        var result = new GoLiveMappingFluentValidation().Validate(request);
        if (!result.IsValid) throw new OnValidationException(result.Errors.Select(x => x.ErrorMessage).ToList());
        return runner.Run<object, ResponseGoLiveImportJson>("POST /go-live/imports/{id}/mapping", new { Id = id, Request = request }, key,
            (actor, token) => Task.FromResult<IReadOnlyCollection<Guid>>([actor.BranchId]),
            async (actor, token) =>
            {
                var row = await Load(actor.BranchId, id, token, true);
                if (row.Status == "Activated" || request.ExpectedRevision != row.MappingRevision)
                    throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                var source = Source(row);
                foreach (var resolution in request.Resolutions)
                {
                    if (source.Find(resolution.Table, resolution.SourceId) is null)
                        throw new OnValidationException([ResourcesErrorMessages.GOLIVE_SOURCE_INVALID]);
                    if (resolution.SourceTerminalId is { } sourceTerminal)
                    {
                        if (resolution.Table != "TblContas" || source.Find("TblContas", sourceTerminal)?["con_tipo"] != "Terminal"
                            || source.Row("TblContas", resolution.SourceId)["con_tipo"] != "Fiado")
                            throw new OnValidationException([ResourcesErrorMessages.GOLIVE_SOURCE_INVALID]);
                    }
                    await ValidateTargets(actor.BranchId, resolution, token);
                }
                foreach (var boundary in request.Resolutions.Where(x => x.Table == "TblConfiguracao" && x.Action == "LockBoundary"))
                    ValidateSourceLock(boundary, source.Row("TblConfiguracao", boundary.SourceId));
                row.SourceLockDate = request.Resolutions.Where(x => x.Table == "TblConfiguracao" && x.Action == "LockBoundary" && x.SourceDate.HasValue).Select(x => x.SourceDate).DefaultIfEmpty(null).Max();
                await Audit(row, actor.UserId, "MappingBefore", row.MappingJson, token);
                row.MappingJson = JsonSerializer.Serialize(request.Resolutions, Json);
                row.MappingRevision++;
                await Audit(row, actor.UserId, "Mapping", row.MappingJson, token);
                row.PreviewId = null; row.PreviewJson = null; row.TargetFingerprint = null;
                row.Status = "Mapped"; row.UpdatedByUserId = actor.UserId; row.UpdatedAt = DateTime.UtcNow;
                return (row.Id, await Response(row));
            }, ct, coordinatePeople: true);
    }

    private async Task ValidateTargets(Guid branchId, RequestGoLiveResolutionJson resolution, CancellationToken ct)
    {
        if(resolution.Action=="Exclude") return;
        if (resolution.Action == "Map")
        {
            var valid = resolution.Table switch
            {
                "TblContas" => resolution.AccountId.HasValue || resolution.OperatorId.HasValue,
                "TblClientes" => resolution.ClientId.HasValue,
                "TblTipoCategoria" => resolution.TransactionTypeId.HasValue,
                "TblProduto" => resolution.ProductId.HasValue != resolution.Field.HasValue,
                "TblUsuario" => resolution.OperatorId.HasValue,
                "TblItemRel" => resolution.PayCategoryId.HasValue,
                _ => false
            };
            if (!valid) throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
        }
        if (resolution.PayCategoryId is { } categoryId) await people.ValidatePayCategory(branchId, categoryId, ct);
        if (resolution.TerminalId is { } terminalId && await terminals.GetActiveByIdAndBranchIdAsNoTracking(terminalId, branchId, ct) is null)
            throw new NotFoundException(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        if (resolution.ProductId is { } p && await products.GetActiveByIdAndBranchIdAsNoTracking(p, branchId) is null)
            throw new NotFoundException(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        if (resolution.TerminalId is { } terminal && resolution.AccountId is { } mappedAccount)
        {
            var account = await accounts.GetActiveByIdAndBranchIdAsNoTracking(mappedAccount, branchId, ct);
            if (account?.TerminalId != terminal) throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
        }
        if (resolution.AccountId is { } a && await accounts.GetActiveByIdAndBranchIdAsNoTracking(a, branchId, ct) is null)
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        if (resolution.ClientId is { } c && await clients.GetActiveByIdAndBranchIdAsNoTracking(c, branchId) is null)
            throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        if (resolution.OperatorId is { } o && await operators.GetActiveByIdAndBranchIdAsNoTracking(o, branchId, ct) is null)
            throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        if (resolution.TransactionTypeId is { } t)
        {
            var type = await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(t, branchId, ct);
            if (type is null || type.SystemRole is not null)
                throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        }
    }

    public Task<ResponseGoLiveImportJson> Preview(Guid id, int expectedRevision, string? key, CancellationToken ct) =>
        runner.Run<object, ResponseGoLiveImportJson>("POST /go-live/imports/{id}/preview", new { Id = id, Revision = expectedRevision }, key,
            async (actor, token) => await AccountKeys(actor, id, token),
            async (actor, token) =>
            {
                var row = await Load(actor.BranchId, id, token, true);
                if (row.Status == "Activated" || row.MappingRevision != expectedRevision)
                    throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                var source = Source(row);
                var resolutions = Resolutions(row);
                await imports.LockTargets(actor.BranchId, token);
                var preview = new ResponseGoLivePreviewJson
                {
                    ToolVersion = ToolVersion,
                    Id = Guid.NewGuid(),
                    MappingRevision = row.MappingRevision,
                    TargetFingerprint = await imports.Fingerprint(actor.BranchId, token),
                    Days = AccessFinancialReplay.Compare(source.Tables, resolutions.Where(x => x.Table == "TblContas" && x.Action == "Map" && x.SourceTerminalId is not null).ToDictionary(x => x.SourceId, x => x.SourceTerminalId!))
                };
                DescribeSourceExceptions(source, resolutions, preview);
                var lookup = resolutions.ToDictionary(x => (x.Table, x.SourceId));
                foreach (var table in new[] { "TblLancamentos", "TblEstoque", "TblRegistroPonto", "TblMovimentacaoBancoHoras", "TblRelMensal", "TblLoteriaEspecial", "TblConfiguracao" })
                {
                    foreach (var record in source.Tables.GetValueOrDefault(table) ?? [])
                    {
                        var sourceId = record.First().Value;
                        lookup.TryGetValue((table, sourceId), out var resolution);
                        if (resolution is not null) resolution = ResolveReferences(resolution, record, lookup);
                        if (resolution?.Action == "Exclude" && table != "TblConfiguracao")
                        {
                            if (resolution.ExclusionDisposition == "UnresolvedHistory")
                            {
                                preview.QuarantinedRows++;
                                preview.Issues.Add(new ResponseGoLiveIssueJson { Table = table, SourceId = sourceId, Code = "quarantined-history", Message = "Histórico sem resolução permanece somente leitura e bloqueia este recorte de implantação." });
                            }
                            else
                            {
                                preview.ExcludedRows++;
                                preview.ExplainedIssues.Add(new ResponseGoLiveIssueJson { Table = table, SourceId = sourceId, Code = "reviewed-exclusion", Message = resolution.Reason });
                            }
                            continue;
                        }
                        var issue = new ResponseGoLiveIssueJson { Table = table, SourceId = sourceId };
                        if (table == "TblConfiguracao")
                        {
                            try
                            {
                                if (resolution is null || resolution.Action != "LockBoundary") throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
                                ValidateSourceLock(resolution, record); preview.SupportedRows++; continue;
                            }
                            catch (ServerException error)
                            { issue.Code = "source-lock-resolution-required"; issue.Message = string.Join(" ", error.GetErrorMessages); issue.SourceValue = record["conf_dtfechamento"]; preview.Issues.Add(issue); continue; }
                        }
                        if (resolution is null)
                        {
                            issue.Code = "mapping-required"; issue.Message = "Mapeie ou exclua este registro com motivo.";
                        }
                        else
                        {
                            try { await ValidateTargets(actor.BranchId, resolution, token); }
                            catch (ServerException) { issue.Code = "target-unavailable"; issue.Message = "O destino mudou ou não está ativo nesta filial."; }
                            if (issue.Code.Length == 0 && GoLivePeopleStage.Supported(resolution))
                            {
                                try
                                {
                                    await people.Validate(actor.BranchId, resolution, record, token);
                                    if (resolution.Action == "Attendance") preview.AttendanceComparisons.Add(await people.CompareAttendance(actor.BranchId, resolution, record, token));
                                    preview.HistoricalPlan.Add(resolution); preview.SupportedRows++; continue;
                                }
                                catch (ServerException error)
                                { issue.Code = table == "TblRelMensal" && resolution.Year is null ? "pay-year-required" : "people-resolution-required"; issue.Message = string.Join(" ", error.GetErrorMessages); }
                            }
                            if (issue.Code.Length == 0 && GoLiveHistoryStage.Supported(resolution))
                            {
                                try
                                {
                                    await history.Validate(actor.BranchId, resolution, record, token);
                                    preview.HistoricalPlan.Add(resolution); preview.SupportedRows++; continue;
                                }
                                catch (ServerException error)
                                { issue.Code = error.GetErrorMessages.Contains(ResourcesErrorMessages.GOLIVE_MONEY_PRECISION_REQUIRED) ? "precision-resolution-required" : "history-resolution-required"; issue.Message = string.Join(" ", error.GetErrorMessages); issue.SourceValue = record.GetValueOrDefault("est_valor") ?? record.GetValueOrDefault("lco_valor"); }
                            }
                            if (issue.Code.Length == 0 && table == "TblLancamentos" && resolution.Action is "OpeningFiado" or "OpeningCheque")
                            {
                                var raw = record["lco_valor"];
                                if (!string.IsNullOrEmpty(record["lco_data_pagamento"]) || record["lco_sinal"] != "Negativo"
                                    || resolution.Action == "OpeningFiado" && record["id_tipo"] != "2")
                                { issue.Code = "origin-or-paid-evidence-conflict"; issue.Message = "A origem ou o pagamento impede tratar o registro como obrigação em aberto."; }
                                if (issue.Code.Length > 0) { preview.Issues.Add(issue); continue; }
                                if (!decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var sourceValue))
                                { issue.Code = "invalid-source-value"; issue.Message = "Valor de origem inválido."; }
                                else if (resolution.TargetValue is null && (sourceValue != decimal.Round(sourceValue, 2) || raw.Contains('.') && raw.Split('.')[1].Skip(2).Any(c => c != '0')))
                                { issue.Code = "precision-resolution-required"; issue.Message = "Informe o valor de destino e justifique a normalização."; issue.SourceValue = raw; }
                                else if (!DateTime.TryParseExact(record["lco_data"], new[] { "yyyy/MM/dd H:mm:ss", "yyyy/MM/dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                                    || !DateTime.TryParseExact(record["lco_vencimento"], new[] { "yyyy/MM/dd H:mm:ss", "yyyy/MM/dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var due))
                                { issue.Code = "date-evidence-required"; issue.Message = "Data e vencimento precisam de evidência válida."; }
                                else
                                {
                                    var plan = new RequestGoLiveOpeningJson
                                    {
                                        AccountId = resolution.AccountId ?? Guid.Empty,
                                        ClientId = resolution.ClientId,
                                        RecordedByOperatorId = resolution.OperatorId ?? Guid.Empty,
                                        TransactionTypeId = resolution.TransactionTypeId ?? Guid.Empty,
                                        AsOfDate = date.Date,
                                        IsCheque = resolution.Action == "OpeningCheque",
                                        SourceReference = row.SourceDigest[..16] + ":" + sourceId,
                                        SourceEvidence = resolution.Reason,
                                        Items = [new() { Value = resolution.TargetValue ?? sourceValue, DueDate = due.Date }]
                                    };
                                    if (!new GoLiveOpeningFluentValidation().Validate(plan).IsValid)
                                    { issue.Code = "opening-evidence-required"; issue.Message = "Resolva conta, cliente, operador, tipo, valor e evidência da obrigação."; }
                                    else
                                    {
                                        try { await openings.Validate(actor, plan, token); preview.OpeningPlan.Add(plan); preview.SupportedRows++; continue; }
                                        catch (ServerException error) { issue.Code = "opening-target-or-lock-conflict"; issue.Message = string.Join(" ", error.GetErrorMessages); }
                                    }
                                }
                            }
                            else if (issue.Code.Length == 0)
                            {
                                issue.Code = table == "TblRelMensal" && resolution.Year is null ? "pay-year-required" : "history-resolution-required";
                                issue.Message = "Este histórico exige resolução de origem, custódia e sequência antes da ativação.";
                            }
                        }
                        preview.Issues.Add(issue);
                    }
                }
                // Preserve cheque plan origins only when every sibling is explicitly reviewed and compatible.
                var openingRows = source.Tables["TblLancamentos"].Where(x => x["lco_origem"].Length > 0)
                    .GroupBy(x => x["lco_origem"]);
                var openingByReference = preview.OpeningPlan.ToDictionary(p => p.SourceReference);
                var mergedOpenings = new HashSet<RequestGoLiveOpeningJson>();
                foreach (var group in openingRows)
                {
                    var candidates = group.Select(r => openingByReference.GetValueOrDefault(row.SourceDigest[..16] + ":" + r.First().Value)).Where(p => p is not null).Cast<RequestGoLiveOpeningJson>().ToList();
                    if (candidates.Count == 0) continue;
                    var first = candidates[0];
                    if (!System.Text.RegularExpressions.Regex.IsMatch(group.Key, "^[1-9][0-9]{0,9}$") || !first.IsCheque || candidates.Count != group.Count() || candidates.Count > 120
                        || candidates.Any(p => !p.IsCheque || p.AccountId != first.AccountId || p.ClientId != first.ClientId || p.RecordedByOperatorId != first.RecordedByOperatorId || p.TransactionTypeId != first.TransactionTypeId || p.AsOfDate != first.AsOfDate))
                    {
                        preview.Issues.Add(new ResponseGoLiveIssueJson { Table = "TblLancamentos", SourceId = group.Key, Code = "opening-plan-conflict", Message = "Revise todas as parcelas da mesma origem, conta, cliente, tipo e data antes da ativação." });
                        continue;
                    }
                    if (candidates.Count > 1)
                    {
                        first.SourceReference = row.SourceDigest[..16] + ":plan:" + group.Key;
                        first.SourceEvidence = string.Join("; ", candidates.Select(p => p.SourceEvidence).Distinct())[..Math.Min(1000, string.Join("; ", candidates.Select(p => p.SourceEvidence).Distinct()).Length)];
                        first.Items = candidates.SelectMany(p => p.Items).ToList();
                        foreach (var other in candidates.Skip(1)) mergedOpenings.Add(other);
                    }
                }
                preview.OpeningPlan.RemoveAll(mergedOpenings.Contains);
                var importedDebts = preview.HistoricalPlan.Where(h => h.Action == "ImportedDebt").Select(h => (h.AccountId, h.ClientId)).ToHashSet();
                if (preview.OpeningPlan.Any(p => !p.IsCheque && importedDebts.Contains((p.AccountId, p.ClientId))))
                    preview.Issues.Add(new ResponseGoLiveIssueJson { Code = "mixed-debt-cutover", Message = "Não some saldo inicial e dívida histórica do mesmo cliente/conta no mesmo recorte." });
                foreach (var duplicate in preview.OpeningPlan.Where(p => !p.IsCheque).GroupBy(p => (p.AccountId, p.ClientId)).Where(g => g.Count() > 1))
                    preview.Issues.Add(new ResponseGoLiveIssueJson { Code = "duplicate-opening-identity", Message = "Consolide o saldo inicial único por conta e cliente com evidência, ou importe a dívida histórica individual." });
                await history.ValidatePlan(actor.BranchId, source, preview, token);
                await history.ApplyReviewedComparison(actor.BranchId, source, preview, token);
                try { await people.ValidatePlan(actor.BranchId, source, preview.HistoricalPlan.Where(GoLivePeopleStage.Supported).ToList(), token); }
                catch (ServerException error) { preview.Issues.Add(new ResponseGoLiveIssueJson { Code = "people-chain-required", Message = string.Join(" ", error.GetErrorMessages) }); }
                preview.CanActivate = preview.Issues.Count == 0 && preview.SupportedRows > 0;
                await Audit(row, actor.UserId, "Preview", JsonSerializer.Serialize(preview, Json), token);
                row.PreviewId = preview.Id; row.TargetFingerprint = preview.TargetFingerprint;
                row.PreviewJson = JsonSerializer.Serialize(preview, Json); row.Status = preview.CanActivate ? "ReviewedPreview" : "NeedsResolution";
                row.UpdatedByUserId = actor.UserId; row.UpdatedAt = DateTime.UtcNow;
                return (row.Id, await Response(row));
            }, ct, coordinatePeople: true);

    private async Task<IReadOnlyCollection<Guid>> AccountKeys(BranchUser actor, Guid id, CancellationToken ct)
    {
        var row = await Load(actor.BranchId, id, ct);
        return [actor.BranchId, .. Resolutions(row).Where(x => x.AccountId.HasValue).Select(x => x.AccountId!.Value).Distinct()];
    }

    public Task<ResponseGoLiveImportJson> Activate(Guid id, RequestActivateGoLiveImportJson request, string? key, CancellationToken ct)
    {
        var validation = new ActivateGoLiveImportFluentValidation().Validate(request);
        if (!validation.IsValid) throw new OnValidationException(validation.Errors.Select(x => x.ErrorMessage).ToList());
        return runner.Run<object, ResponseGoLiveImportJson>("POST /go-live/imports/{id}/activate", new { Id = id, Request = request }, key,
            async (actor, token) => await AccountKeys(actor, id, token),
            async (actor, token) =>
            {
                var row = await Load(actor.BranchId, id, token, true);
                if (request.DryRunVersion != ToolVersion || row.SchemaVersion != "access-csv-1" || row.MappingRevision != request.MappingRevision || row.PreviewId != request.PreviewId
                    || row.SourceDigest != request.SourceDigest || row.TargetFingerprint != request.TargetFingerprint)
                    throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                if (row.Status == "Activated") return (row.Id, await Response(row));
                if (await imports.HasActivated(actor.BranchId, token)) throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                var preview = JsonSerializer.Deserialize<ResponseGoLivePreviewJson>(row.PreviewJson ?? "null", Json);
                if (preview?.CanActivate != true || preview.ToolVersion != request.DryRunVersion) throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED);
                await imports.LockTargets(actor.BranchId, token);
                if (await imports.Fingerprint(actor.BranchId, token) != request.TargetFingerprint)
                    throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                foreach (var resolution in Resolutions(row)) await ValidateTargets(actor.BranchId, resolution, token);
                var ids = new List<Guid>();
                ids.AddRange(await history.Stage(actor, row, Source(row), preview.HistoricalPlan.Where(GoLiveHistoryStage.Supported).ToList(), token));
                ids.AddRange(await people.Stage(actor, row, Source(row), preview.HistoricalPlan.Where(GoLivePeopleStage.Supported).ToList(), token));
                var included = Resolutions(row).Where(x => x.Table == "TblLancamentos" && x.Action is "OpeningFiado" or "OpeningCheque").ToDictionary(x => x.SourceId);
                var membersByReference = new Dictionary<string, List<RequestGoLiveResolutionJson>>();
                foreach (var record in Source(row).Tables["TblLancamentos"])
                {
                    if (!included.TryGetValue(record.First().Value, out var resolution)) continue;
                    foreach (var reference in new[] { row.SourceDigest[..16] + ":" + resolution.SourceId, row.SourceDigest[..16] + ":plan:" + record["lco_origem"] })
                    {
                        if (!membersByReference.TryGetValue(reference, out var members)) membersByReference[reference] = members = [];
                        members.Add(resolution);
                    }
                }
                foreach (var plan in preview.OpeningPlan)
                {
                    var (_, response) = await openings.Stage(actor, plan, token);
                    ids.AddRange(response.TransactionIds);
                    var members = membersByReference.GetValueOrDefault(plan.SourceReference) ?? [];
                    if (members.Count != response.TransactionIds.Count) throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                    for (var i = 0; i < members.Count; i++)
                        await imports.Register(new GoLiveImportTarget
                        {
                            BranchId = actor.BranchId,
                            GoLiveImportId = row.Id,
                            SourceTable = members[i].Table,
                            SourceId = members[i].SourceId,
                            TargetId = response.TransactionIds[i],
                            TargetKind = members[i].Action
                        }, token);
                }
                await Audit(row, actor.UserId, "Activate", JsonSerializer.Serialize(request, Json), token);
                if (row.SourceLockDate is { } sourceLock)
                {
                    var setting = await settings.GetByBranchId(actor.BranchId, token) ?? throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                    if (setting.LockDate < sourceLock) setting.LockDate = sourceLock;
                }
                foreach (var mapping in Resolutions(row).Where(x => x.Action is "Map" or "LockBoundary"))
                {
                    var targets = new List<(Guid Id, string Kind)>();
                    if (mapping.AccountId is { } a) targets.Add((a, "Account"));
                    if (mapping.ClientId is { } c) targets.Add((c, "Client"));
                    if (mapping.OperatorId is { } o) targets.Add((o, "Operator"));
                    if (mapping.ProductId is { } p) targets.Add((p, "Product"));
                    if (mapping.TransactionTypeId is { } t) targets.Add((t, "TransactionType"));
                    if (mapping.PayCategoryId is { } pc) targets.Add((pc, "PayCategory"));
                    if (mapping.Action == "LockBoundary")
                    {
                        var setting = await settings.GetByBranchId(actor.BranchId, token) ?? throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
                        targets.Add((setting.Id, "LockBoundary"));
                    }
                    // The primary source identity has one canonical destination; auxiliary links remain in the immutable mapping evidence.
                    if (targets.Count > 0)
                    {
                        await imports.Register(new GoLiveImportTarget { BranchId = actor.BranchId, GoLiveImportId = row.Id, SourceTable = mapping.Table, SourceId = mapping.SourceId, TargetId = targets[0].Id, TargetKind = targets[0].Kind }, token);
                    }
                }
                row.ResultJson = JsonSerializer.Serialize(ids.Distinct().ToList(), Json); row.Status = "Activated";
                row.UpdatedByUserId = actor.UserId; row.UpdatedAt = DateTime.UtcNow;
                return (row.Id, await Response(row));
            }, ct, coordinatePeople: true);
    }

    private static void DescribeSourceExceptions(AccessSource source, List<RequestGoLiveResolutionJson> resolutions, ResponseGoLivePreviewJson preview)
    {
        var tables = source.Tables;
        var reviewed = resolutions.ToDictionary(x => (x.Table, x.SourceId));
        void Explain(string table, Dictionary<string, string> row, string code, string message, string? value = null)
        {
            var id = row.First().Value;
            var resolved = reviewed.TryGetValue((table, id), out var r);
            var issue = new ResponseGoLiveIssueJson { Table = table, SourceId = id, Code = code, Message = message, SourceValue = value };
            if (resolved) preview.ExplainedIssues.Add(issue); else preview.Issues.Add(issue);
        }
        foreach (var row in tables["TblLancamentos"])
        {
            var type = source.Find("TblTipoCategoria", row["id_tipo"]);
            var category = source.Find("TblCategoria", row["id_categoria"]);
            if (type is not null && type["id_categoria"] != row["id_categoria"])
                Explain("TblLancamentos", row, "source-category-mismatch", "A categoria de origem diverge da categoria do tipo. A resolução explícita conserva esta exceção.", row["id_categoria"]);
            if (category is not null && category["cat_sinal"] != row["lco_sinal"])
                Explain("TblLancamentos", row, "source-sign-mismatch", "O sinal da origem diverge da categoria. Confira o sinal e o tipo escolhido.", row["lco_sinal"]);
            if (type is null || category is null || source.Find("TblContas", row["id_conta"]) is null
                || row["id_cliente"].Length > 0 && source.Find("TblClientes", row["id_cliente"]) is null)
                Explain("TblLancamentos", row, "orphan-source-reference", "Há referência ausente no catálogo da origem. A associação de destino ou exclusão precisa de evidência explícita.");
            if (System.Text.RegularExpressions.Regex.IsMatch(row["lco_descricao"], "(?:^|\\W)(?:teste|test|exemplo|synthetic)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                Explain("TblLancamentos", row, "test-like-source", "A descrição contém indicação de teste; revise inclusão ou exclusão.");
        }
        foreach (var duplicate in tables["TblEstoque"].GroupBy(x => (x["con_id"], x["est_data"], x["prod_id"])).Where(g => g.Count() > 1))
            foreach (var row in duplicate) Explain("TblEstoque", row, "duplicate-source-count-slot", "Mais de uma contagem ocupa o mesmo produto, caixa e instante de origem. Revise o recorte e a sequência.");
        foreach (var row in tables["TblLogExclusao"])
            preview.ExplainedIssues.Add(new ResponseGoLiveIssueJson { Table = "TblLogExclusao", SourceId = row.First().Value, Code = "readonly-deletion-evidence", Message = "Registro de exclusão preservado somente como evidência; não recria dinheiro nem apaga destinos." });
    }

    private static List<RequestGoLiveResolutionJson> Suggestions(Dictionary<string, List<Dictionary<string, string>>> tables)
    {
        var suggestions = new List<RequestGoLiveResolutionJson>();
        foreach (var row in tables.GetValueOrDefault("TblEstoque") ?? [])
        {
            var product = row["prod_id"];
            var proposed = new RequestGoLiveResolutionJson { Table = "TblEstoque", SourceId = row["est_id"], Reason = "Sugestão do registro M9.5; confirme a interpretação e a evidência antes de salvar." };
            if (product is "1" or "8" or "9" or "10" or "13")
            { proposed.Action = "Exclude"; proposed.ExclusionDisposition = "KnownIrrelevant"; proposed.Reason = "Sugestão M9.5: slot legado não físico ou diferença histórica preservada como evidência; confirmar exclusão do efeito operacional."; }
            else if (product is "7" or "12" or "14")
            { proposed.Action = "SessionField"; proposed.Field = product switch { "7" => SessionCloseField.BolaoFee, "12" => SessionCloseField.TerminalReport, _ => SessionCloseField.SpecialFee }; }
            else { proposed.Action = "CountedSession"; }
            suggestions.Add(proposed);
        }
        foreach (var row in tables.GetValueOrDefault("TblTipoCategoria") ?? [])
            suggestions.Add(new RequestGoLiveResolutionJson { Table = "TblTipoCategoria", SourceId = row["tipo_id"], Action = "Map", TargetName = row["tipo_nome"], Reason = "Sugestão de correspondência de tipo M9.5; escolha o destino real e confirme categoria e sinal." });
        return suggestions;
    }

    private void ValidateSourceLock(RequestGoLiveResolutionJson resolution, Dictionary<string, string> source)
    {
        if (!resolution.HistoryComplete || resolution.SourceDate is { } chosen && (chosen.TimeOfDay != TimeSpan.Zero || chosen > clock.LocalBusinessDate(clock.UtcNow())))
            throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
        if (!string.IsNullOrEmpty(source["conf_dtfechamento"]))
        {
            var original = GoLiveHistoryStage.Date(source["conf_dtfechamento"]);
            if (resolution.SourceDate is null || resolution.SourceDate != original) throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
        }
        else if (resolution.SourceDate is not null) throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
    }

    private static RequestGoLiveResolutionJson ResolveReferences(RequestGoLiveResolutionJson original, Dictionary<string, string> record, Dictionary<(string Table, string SourceId), RequestGoLiveResolutionJson> lookup)
    {
        var row = JsonSerializer.Deserialize<RequestGoLiveResolutionJson>(JsonSerializer.Serialize(original, Json), Json)!;
        RequestGoLiveResolutionJson? Reference(string table, string? id) => id is not null && lookup.TryGetValue((table, id), out var mapped) && mapped.Action == "Map" ? mapped : null;
        var account = Reference("TblContas", record.GetValueOrDefault("id_conta") ?? record.GetValueOrDefault("con_id") ?? record.GetValueOrDefault("rel_idconta"));
        row.AccountId ??= account?.AccountId;
        row.TerminalId ??= account?.TerminalId;
        row.ProductId ??= Reference("TblProduto", record.GetValueOrDefault("prod_id"))?.ProductId;
        row.Field ??= Reference("TblProduto", record.GetValueOrDefault("prod_id"))?.Field;
        row.ClientId ??= Reference("TblClientes", record.GetValueOrDefault("id_cliente"))?.ClientId;
        row.PayCategoryId ??= Reference("TblItemRel", record.GetValueOrDefault("Id_item"))?.PayCategoryId;
        if (row.Table is "TblRelMensal" or "TblMovimentacaoBancoHoras" or "TblRegistroPonto") row.OperatorId ??= account?.OperatorId;
        row.TransactionTypeId ??= Reference("TblTipoCategoria", record.GetValueOrDefault("id_tipo"))?.TransactionTypeId;
        return row;
    }

    private Task Audit(GoLiveImport row, Guid actorId, string action, string evidence, CancellationToken ct) =>
        imports.Audit(new GoLiveImportAudit
        {
            GoLiveImportId = row.Id,
            ActorUserId = actorId,
            MappingRevision = row.MappingRevision,
            Action = action,
            EvidenceJson = evidence
        }, ct);

    internal static void EnsureSameInterpretation(RequestGoLiveImportJson recorded, RequestGoLiveImportJson requested)
    {
        if (recorded.Profile != requested.Profile || recorded.Encoding != requested.Encoding)
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_INTERPRETATION_CONFLICT);
    }

    private AccessSource Source(GoLiveImport row)
    {
        if (sourceCache.TryGetValue(row.Id, out var cached) && cached.Evidence == row.SourceJson) return cached.Source;
        var source = AccessSourceReader.Read(JsonSerializer.Deserialize<RequestGoLiveImportJson>(row.SourceJson, Json)!);
        sourceCache[row.Id] = (row.SourceJson, source);
        return source;
    }
    private static List<RequestGoLiveResolutionJson> Resolutions(GoLiveImport row) => JsonSerializer.Deserialize<List<RequestGoLiveResolutionJson>>(row.MappingJson, Json)!;
    private async Task<ResponseGoLiveImportJson> Response(GoLiveImport row, bool detail = true)
    {
        var tables = detail ? new Dictionary<string, List<Dictionary<string, string>>>(Source(row).Tables) : [];
        // Passwords/authentication fields are raw evidence only, never exposed as mapping candidates.
        if (tables.TryGetValue("TblUsuario", out var users))
            tables["TblUsuario"] = users.Select(user => user.Where(field => field.Key is not "usu_senha" and not "usu_autenticar").ToDictionary()).ToList();
        return new ResponseGoLiveImportJson
        {
            Id = row.Id,
            SchemaVersion = row.SchemaVersion,
            SourceDigest = row.SourceDigest,
            Profile = row.Profile,
            Status = row.Status,
            SourceLockDate = row.SourceLockDate,
            MappingRevision = row.MappingRevision,
            PreviewId = row.PreviewId,
            TargetFingerprint = row.TargetFingerprint,
            Suggestions = detail ? Suggestions(tables) : [],
            Resolutions = detail ? Resolutions(row) : [],
            Tables = tables,
            Preview = detail && row.PreviewJson is not null ? JsonSerializer.Deserialize<ResponseGoLivePreviewJson>(row.PreviewJson, Json) : null,
            Targets = detail && row.Status == "Activated" ? (await imports.Targets(row.BranchId, row.Id, CancellationToken.None)).Select(x => new ResponseGoLiveImportTargetJson { SourceTable = x.SourceTable, SourceId = x.SourceId, TargetId = x.TargetId, TargetKind = x.TargetKind }).ToList() : [],
            TargetIds = row.ResultJson is null ? [] : JsonSerializer.Deserialize<List<Guid>>(row.ResultJson, Json)!
        };
    }
}

public class GoLiveMappingFluentValidation : AbstractValidator<RequestGoLiveMappingJson>
{
    public GoLiveMappingFluentValidation()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
        RuleFor(x => x.Resolutions).NotNull().Must(x => x is not null && x.All(r => r is not null) && x.Count <= AccessSourceReader.MaxRows
            && x.Select(r => (r.Table, r.SourceId)).Distinct().Count() == x.Count).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleForEach(x => x.Resolutions).SetValidator(new GoLiveResolutionFluentValidation());
    }
}

public class GoLiveResolutionFluentValidation : AbstractValidator<RequestGoLiveResolutionJson>
{
    public GoLiveResolutionFluentValidation()
    {
        RuleFor(x=>x).Must(x=>x.Table switch {
            "TblLancamentos"=>x.Action is "Transaction" or "ImportedDebt" or "OpeningFiado" or "OpeningCheque" or "Exclude",
            "TblEstoque"=>x.Action is "CountedSession" or "SessionField" or "Exclude",
            "TblRegistroPonto"=>x.Action is "Attendance" or "Exclude",
            "TblMovimentacaoBancoHoras"=>x.Action is "HourOpening" or "HourPayment" or "HourAdjustment" or "AttendanceEvidence" or "Exclude",
            "TblRelMensal"=>x.Action is "PayItem" or "Exclude",
            "TblLoteriaEspecial"=>x.Action is "SpecialDraw" or "Exclude",
            "TblConfiguracao"=>x.Action=="LockBoundary",
            "TblContas" or "TblClientes" or "TblTipoCategoria" or "TblProduto" or "TblUsuario" or "TblItemRel"=>x.Action is "Map" or "Exclude",
            _=>x.Action=="Exclude"
        }).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.Table).NotEmpty().MaximumLength(80).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.SourceId).NotEmpty().MaximumLength(30).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.Action).Must(x => x is "Exclude" or "Map" or "OpeningFiado" or "OpeningCheque" or "CountedSession" or "Transaction" or "ImportedDebt" or "SessionField" or "PayItem" or "HourOpening" or "HourPayment" or "HourAdjustment" or "Attendance" or "AttendanceEvidence" or "SpecialDraw" or "LockBoundary").WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.ExclusionDisposition).Must(x => x is "KnownIrrelevant" or "OutsideScope" or "UnresolvedHistory").When(x => x.Action == "Exclude").WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.TargetValue).Must(x => x is null || decimal.Abs(x.Value) <= 999999999999.99m && decimal.Round(x.Value, 2) == x).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.PayKind).IsInEnum().WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.AttendanceStatus).IsInEnum().WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.TargetHours).Must(x => x is null || decimal.Round(x.Value, 4) == x && decimal.Abs(x.Value) <= 99999999m).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.Field).IsInEnum().WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
        RuleFor(x => x.Year).Must(x => x is null or >= 1 and <= 9999).WithMessage(ResourcesErrorMessages.GOLIVE_SOURCE_INVALID);
    }
}

public class ActivateGoLiveImportFluentValidation : AbstractValidator<RequestActivateGoLiveImportJson>
{
    public ActivateGoLiveImportFluentValidation()
    {
        RuleFor(x => x.MappingRevision).GreaterThan(0).WithMessage(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
        RuleFor(x => x.PreviewId).NotEmpty().WithMessage(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
        RuleFor(x => x.SourceDigest).Matches("^[0-9a-f]{64}$").WithMessage(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
        RuleFor(x => x.TargetFingerprint).Matches("^[0-9a-f]{64}$").WithMessage(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
        RuleFor(x => x.DryRunVersion).Equal(GoLiveImportUseCase.ToolVersion).WithMessage(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
    }
}
