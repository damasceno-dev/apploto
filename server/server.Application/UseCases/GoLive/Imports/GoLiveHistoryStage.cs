using System.Globalization;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Imports;

/// <summary>Validated import boundary. No integrity trigger is disabled and no historical acknowledgment is invented.</summary>
public class GoLiveHistoryStage(
    IGoLiveHistoryRepository history,
    IGoLiveImportsRepository imports,
    IGoLiveOpeningsRepository openings,
    IAccountsRepository accounts,
    IProductsRepository products,
    IOperatorsRepository operators,
    IClientsRepository clients,
    ITransactionTypesRepository types,
    ITransactionsRepository transactions,
    ISettlementEventsRepository events,
    LockDateGuard locks,
    IBranchClock clock,
    IBankingReadLocks readLocks,
    IUnitOfWork unitOfWork)
{
    internal static bool Supported(RequestGoLiveResolutionJson r) =>
        r.Table == "TblEstoque" && r.Action is "CountedSession" or "SessionField" || r.Table == "TblLancamentos" && r.Action is "Transaction" or "ImportedDebt";

    internal async Task Validate(Guid branchId, RequestGoLiveResolutionJson resolution, Dictionary<string, string> source, CancellationToken ct)
    {
        NormalizeIntervals(resolution);
        var account = await accounts.GetActiveByIdAndBranchIdAsNoTracking(resolution.AccountId ?? Guid.Empty, branchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        var op = await operators.GetActiveByIdAndBranchIdAsNoTracking(resolution.OperatorId ?? Guid.Empty, branchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        var date = Date(source[resolution.Table == "TblEstoque" ? "est_data" : "lco_data"]);
        if (date > clock.LocalBusinessDate(clock.UtcNow())) Invalid();
        await locks.EnsureNotLocked(branchId, date, ResourcesErrorMessages.TRANSACTION_DATE_LOCKED, ct);
        var value = Value(resolution, source[resolution.Table == "TblEstoque" ? "est_valor" : "lco_valor"]);
        if (resolution.Action == "SessionField")
        {
            if (account.Type != AccountType.Terminal || resolution.Sequence is null or < 1 || resolution.Field is null
                || !Enum.IsDefined(resolution.Field.Value) || resolution.Field != SessionCloseField.TerminalReport && value < 0)
                Invalid();
        }
        else if (resolution.Action == "CountedSession")
        {
            if (account.Type != AccountType.Terminal || resolution.TerminalId is null || resolution.TerminalId != account.TerminalId
                || resolution.Sequence is null or < 1 || !resolution.HistoryComplete
                || resolution.StartsAt is not { Kind: DateTimeKind.Utc } start || resolution.EndsAt is not { Kind: DateTimeKind.Utc } end
                || end <= start || end > clock.UtcNow() || clock.LocalBusinessDate(start) != date || clock.LocalBusinessDate(end.AddTicks(-1)) != date)
                Invalid();
            if (await history.HasCashboxHistory(branchId, account.Id, ct)) throw new ConflictException(ResourcesErrorMessages.GOLIVE_IMPORT_STALE);
            var product = await products.GetActiveByIdAndBranchIdAsNoTracking(resolution.ProductId ?? Guid.Empty, branchId);
            if (source["prod_id"] is "1" or "7" or "8" or "9" or "10" or "12" or "13" or "14") Invalid();
            if (product is null || product.Name == "Diferença Caixa" || value < 0) Invalid();
        }
        else
        {
            if (!resolution.HistoryComplete || source["lco_status"].Length > 0
                || source["lco_origem"].Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(source["lco_origem"], "^[1-9][0-9]{0,9}$")) Invalid();
            if (value <= 0 || source["lco_sinal"] is not "Positivo" and not "Negativo") Invalid();
            var type = await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(resolution.TransactionTypeId ?? Guid.Empty, branchId, ct);
            if (type is null || type.SystemRole is not null || !type.Category.Active
                || type.Category.DefaultDirection != (source["lco_sinal"] == "Positivo" ? Direction.In : Direction.Out)) Invalid();
            if (resolution.ClientId is { } clientId && await clients.GetActiveByIdAndBranchIdAsNoTracking(clientId, branchId, ct) is null) Invalid();
            if (resolution.Action == "ImportedDebt")
            {
                if (await openings.Exists(branchId, $"fiado:{account.Id}:{resolution.ClientId}", ct)) throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_EXISTS);
                if (type!.FinancialClass != FinancialClass.CaixaAndClientMoney || type.MonthlyFinancialRole != MonthlyFinancialRole.None || type.SettlementInstrument != SettlementInstrument.None || type.FiadoPaymentKind is not null
                    || source["id_tipo"] != "2" || account.Type != AccountType.Tab || resolution.ClientId is null
                    || !type!.RequiresTabAccountAndClient || type.Category.DefaultDirection != Direction.Out) Invalid();
            }
            else if (source["id_tipo"] == "2" || type!.RequiresTabAccountAndClient && resolution.ClientId is null) Invalid();
            if (account.Type == AccountType.Terminal && resolution.Sequence is null) Invalid();
            _ = Date(source["lco_vencimento"]);
            if (source["lco_data_pagamento"].Length > 0)
            {
                _ = PaidInstant(source["lco_data_pagamento"]);
                var paid = Date(source["lco_data_pagamento"]);
                if (paid < date || paid > clock.LocalBusinessDate(clock.UtcNow())) Invalid();
            }
        }
    }

    internal async Task<IReadOnlyList<Guid>> Stage(
        BranchUser actor,
        GoLiveImport import,
        AccessSource source,
        IReadOnlyList<RequestGoLiveResolutionJson> plan,
        CancellationToken ct)
    {
        // Hold configuration and identities stable; raw evidence never supplies a login or acknowledgment.
        await readLocks.TransactionTypes(actor.BranchId, plan.Where(r => r.TransactionTypeId.HasValue).Select(r => r.TransactionTypeId!.Value).Distinct().Order().ToList(), ct);
        await readLocks.Clients(actor.BranchId, plan.Where(r => r.ClientId.HasValue).Select(r => r.ClientId!.Value).Distinct().Order().ToList(), ct);
        await readLocks.Operators(actor.BranchId, plan.Where(r => r.OperatorId.HasValue).Select(r => r.OperatorId!.Value).Distinct().Order().ToList(), ct);
        foreach (var resolution in plan) await Validate(actor.BranchId, resolution, Row(source, resolution), ct);
        var ids = new List<Guid>();
        var sessions = new Dictionary<(Guid AccountId, int Sequence), DailyClose>();
        foreach (var group in plan.Where(r => r.Action == "CountedSession").GroupBy(r => (AccountId: r.AccountId!.Value, Sequence: r.Sequence!.Value)).OrderBy(g => g.Key.AccountId).ThenBy(g => g.Key.Sequence))
        {
            var first = group.First();
            var date = Date(Row(source, first)["est_data"]);
            if (group.Any(r => r.StartsAt != first.StartsAt || r.EndsAt != first.EndsAt || r.OperatorId != first.OperatorId || r.TerminalId != first.TerminalId || Date(Row(source, r)["est_data"]) != date)
                || group.Select(r => r.ProductId).Distinct().Count() != group.Count()) Invalid();
            var session = new DailyClose
            {
                BranchId = actor.BranchId,
                AccountId = group.Key.AccountId,
                Date = date,
                Sequence = group.Key.Sequence,
                StartedAt = first.StartsAt,
                EndedAt = first.EndsAt,
                CustodianOperatorId = first.OperatorId,
                TerminalId = first.TerminalId,
                MappingState = SessionMappingState.Ready,
                CustodyState = SessionCustodyState.Finished,
                Origin = SessionOrigin.Migrated,
                OpenedByUserId = actor.UserId,
                RecordedByUserId = actor.UserId,
                RecordedByOperatorId = first.OperatorId,
                ItemsFirstRecordedAt = clock.UtcNow(),
                CountRevision = 1,
                Status = DailyCloseStatus.Draft,
                ReviewHistoryComplete = false,
                CustodyHistoryComplete = true,
                Notes = "Histórico importado com custódia explícita; revisão anterior indisponível. " + first.Reason[..Math.Min(first.Reason.Length, 500)]
            };
            foreach (var item in group)
                session.Items.Add(new DailyCloseItem { DailyCloseId = session.Id, ProductId = item.ProductId!.Value, Value = Value(item, Row(source, item)["est_valor"]) });
            await history.Add(session, ct);
            sessions.Add(group.Key, session);
            ids.Add(session.Id);
            foreach (var item in group) await Register(actor, import, item, session.Id, "CountedSession", ct);
        }
        // Insert sessions first: their existing SQL guards bind every cashbox row to its exact branch/account/date.
        await unitOfWork.Commit(ct);
        foreach (var field in plan.Where(r => r.Action == "SessionField"))
        {
            var evidence = Row(source, field);
            if (!sessions.TryGetValue((field.AccountId!.Value, field.Sequence!.Value), out var session) || session.Date != Date(evidence["est_data"])) Invalid();
            var value = Value(field, evidence["est_valor"]);
            var slot = new SessionFieldEffect
            {
                BranchId = actor.BranchId,
                SessionId = session!.Id,
                Field = field.Field!.Value,
                Value = value,
                Revision = 1,
                UpdatedAt = clock.UtcNow(),
                UpdatedByUserId = actor.UserId
            };
            Transaction? effect = null;
            if (value != 0)
            {
                var role = field.Field switch
                {
                    SessionCloseField.BolaoFee => TransactionTypeSystemRole.BolaoFee,
                    SessionCloseField.SpecialFee => TransactionTypeSystemRole.SpecialFee,
                    _ => value > 0 ? TransactionTypeSystemRole.TerminalReportIn : TransactionTypeSystemRole.TerminalReportOut
                };
                var type = await types.GetActiveSystemTypeByBranchIdAsNoTracking(actor.BranchId, role, ct)
                    ?? throw new ConflictException(ResourcesErrorMessages.BRANCH_FINANCIAL_SEEDS_MISSING);
                effect = new Transaction
                {
                    BranchId = actor.BranchId,
                    AccountId = session.AccountId,
                    Date = session.Date,
                    DueDate = session.Date,
                    SessionId = session.Id,
                    Value = decimal.Abs(value),
                    Direction = type.Category.DefaultDirection,
                    CategoryId = type.CategoryId,
                    TransactionTypeId = type.Id,
                    CreatedByUserId = actor.UserId,
                    RecordedByOperatorId = field.OperatorId!.Value,
                    GenerationKind = (TransactionGenerationKind)((int)field.Field.Value + 2)
                };
                await transactions.Add(effect, ct);
                slot.CurrentTransactionId = effect.Id;
                session.LedgerRevision++;
            }
            slot.Revisions.Add(new SessionFieldEffectRevision
            {
                SessionFieldEffectId = slot.Id,
                Revision = 1,
                Value = value,
                CreatedTransactionId = effect?.Id,
                ActorUserId = actor.UserId,
                RecordedAt = clock.UtcNow(),
                Origin = SessionFieldEffectOrigin.MigratedLegacyRow
            });
            await history.Add(slot, ct);
            await Register(actor, import, field, slot.Id, "SessionField", ct);
            ids.Add(slot.Id);
        }
        var planOrigins = plan.Where(r => r.Table == "TblLancamentos").Select(r => Row(source, r)["lco_origem"])
            .Where(x => x.Length > 0).Distinct().ToDictionary(x => x, _ => Guid.NewGuid());
        var usedOrigins = new HashSet<string>();
        var pendingEvents = new List<(Transaction Row, DateTime Paid, DateTime PaidUtc, SettlementInstrument Instrument, bool Tab)>();
        foreach (var resolution in plan.Where(r => r.Table == "TblLancamentos").OrderBy(r => Date(Row(source, r)["lco_data"])).ThenBy(r => r.SourceId, StringComparer.Ordinal))
        {
            var evidence = Row(source, resolution);
            var account = (await accounts.GetActiveByIdAndBranchIdAsNoTracking(resolution.AccountId!.Value, actor.BranchId, ct))!;
            var type = (await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(resolution.TransactionTypeId!.Value, actor.BranchId, ct))!;
            Guid? sessionId = null;
            var date = Date(evidence["lco_data"]);
            if (account.Type == AccountType.Terminal)
            {
                if (!sessions.TryGetValue((account.Id, resolution.Sequence!.Value), out var session) || session.Date != date) Invalid();
                sessionId = session!.Id;
                session.LedgerRevision++;
            }
            var originKey = evidence["lco_origem"];
            Guid? origin = originKey.Length > 0 ? planOrigins[originKey] : null;
            var rowId = origin is { } planRoot && usedOrigins.Add(originKey) ? planRoot : Guid.NewGuid();
            var row = new Transaction
            {
                Id = rowId,
                BranchId = actor.BranchId,
                AccountId = account.Id,
                Date = date,
                DueDate = Date(evidence["lco_vencimento"]),
                Value = Value(resolution, evidence["lco_valor"]),
                ClientId = resolution.ClientId,
                Direction = type.Category.DefaultDirection,
                TransactionTypeId = type.Id,
                CategoryId = type.CategoryId,
                RecordedByOperatorId = resolution.OperatorId!.Value,
                CreatedByUserId = actor.UserId,
                SessionId = sessionId,
                Description = evidence["lco_descricao"][..Math.Min(500, evidence["lco_descricao"].Length)],
                OriginTransactionId = origin
            };
            await transactions.Add(row, ct);
            await Register(actor, import, resolution, row.Id, resolution.Action, ct);
            ids.Add(row.Id);
            if (evidence["lco_data_pagamento"].Length > 0) pendingEvents.Add((row, Date(evidence["lco_data_pagamento"]), PaidInstant(evidence["lco_data_pagamento"]), type.SettlementInstrument, account.Type == AccountType.Tab));
        }
        await unitOfWork.Commit(ct);
        foreach (var (row, paid, paidUtc, instrument, tab) in pendingEvents)
        {
            // Reconstructed paid evidence closes the old claim; it is never a second payment or bank receipt.
            await events.Add(new SettlementEvent
            {
                BranchId = actor.BranchId,
                ReceivableTransactionId = row.Id,
                Sequence = 1,
                Kind = SettlementEventKind.MigratedSettlement,
                Amount = row.Value,
                OutstandingDelta = tab ? 0 : -row.Value,
                OccurredOn = paid,
                PostedOn = paid,
                Provenance = SettlementEventProvenance.MigratedFromPaidAt,
                SourcePaidAt = paidUtc,
                SettlementInstrument = instrument,
                MigrationIssue = tab ? SettlementMigrationIssue.NotSettleableTabRow : SettlementMigrationIssue.None
            }, ct);
        }
        return ids;
    }

    internal async Task ValidatePlan(Guid branchId, AccessSource source, ResponseGoLivePreviewJson preview, CancellationToken ct)
    {
        var groups = preview.HistoricalPlan.Where(r => r.Action == "CountedSession").GroupBy(r => (r.AccountId, r.Sequence)).ToList();
        var countedBySession = groups.ToDictionary(g => g.Key, g => g.First());
        var originCounts = source.Tables["TblLancamentos"].ToLookup(r => r["lco_origem"]);
        var movementsBySession = preview.HistoricalPlan.Where(r => r.Table == "TblLancamentos").ToLookup(r => (r.AccountId, r.Sequence));
        var validSessions = new HashSet<(Guid?, int?, DateTime)>();
        foreach (var account in groups.GroupBy(g => g.Key.AccountId))
        {
            DateTime? priorEnd = null;
            var sequence = 1;
            foreach (var group in account.OrderBy(g => g.Key.Sequence))
            {
                var first = group.First();
                var date = Date(Row(source, first)["est_data"]);
                if (group.Key.Sequence != sequence++ || group.Select(x => x.ProductId).Distinct().Count() != group.Count()
                    || first.StartsAt < priorEnd || group.Any(x => x.StartsAt != first.StartsAt || x.EndsAt != first.EndsAt || x.OperatorId != first.OperatorId || x.TerminalId != first.TerminalId || Date(Row(source, x)["est_data"]) != date))
                    preview.Issues.Add(new ResponseGoLiveIssueJson { Table = first.Table, SourceId = first.SourceId, Code = "session-chain-conflict", Message = "Revise sequência, itens duplicados, custódia e intervalos sobrepostos desta cadeia." });
                validSessions.Add((first.AccountId, first.Sequence, date));
                priorEnd = first.EndsAt;
            }
        }
        foreach (var duplicate in preview.HistoricalPlan.Where(r => r.Action == "SessionField").GroupBy(r => (r.AccountId, r.Sequence, r.Field)).Where(g => g.Count() > 1))
            preview.Issues.Add(new ResponseGoLiveIssueJson { Code = "duplicate-field-effect", Message = "Cada relatório ou taxa pode ter apenas um efeito por turno." });
        foreach (var origin in preview.HistoricalPlan.Where(r => r.Table == "TblLancamentos" && Row(source, r)["lco_origem"].Length > 0).GroupBy(r => Row(source, r)["lco_origem"]))
        {
            var first = origin.First();
            if (origin.Any(r => r.AccountId != first.AccountId || r.ClientId != first.ClientId || r.TransactionTypeId != first.TransactionTypeId || r.Action != first.Action)
                || originCounts[origin.Key].Count() != origin.Count())
                preview.Issues.Add(new ResponseGoLiveIssueJson { Table = "TblLancamentos", SourceId = first.SourceId, Code = "plan-origin-conflict", Message = "A origem deve conter todas as parcelas compatíveis; não misture tipos, contas, clientes ou recortes." });
        }
        foreach (var field in preview.HistoricalPlan.Where(r => r.Action == "SessionField"))
        {
            var counted = countedBySession.GetValueOrDefault((field.AccountId, field.Sequence));
            if (counted is not null && counted.OperatorId != field.OperatorId)
                preview.Issues.Add(new ResponseGoLiveIssueJson { Table = field.Table, SourceId = field.SourceId, Code = "field-custody-conflict", Message = "A taxa ou relatório deve manter a custódia comprovada do turno." });
            foreach (var movement in movementsBySession[(field.AccountId, field.Sequence)])
            {
                var type = await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(movement.TransactionTypeId!.Value, branchId, ct);
                if (field.Field == SessionCloseField.BolaoFee && type?.MonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee
                    || field.Field == SessionCloseField.SpecialFee && type?.MonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee)
                    preview.Issues.Add(new ResponseGoLiveIssueJson { Table = field.Table, SourceId = field.SourceId, Code = "duplicate-legacy-field-effect", Message = "O lançamento legado e o campo do turno reconheceriam a mesma taxa. Revise a exclusão explícita de um efeito." });
            }
        }
        foreach (var row in preview.HistoricalPlan.Where(r => r.Table == "TblLancamentos" || r.Action == "SessionField"))
        {
            var account = await accounts.GetActiveByIdAndBranchIdAsNoTracking(row.AccountId!.Value, branchId, ct);
            if (account?.Type == AccountType.Terminal && !validSessions.Contains((row.AccountId, row.Sequence, Date(Row(source, row)[row.Table == "TblEstoque" ? "est_data" : "lco_data"]))))
                preview.Issues.Add(new ResponseGoLiveIssueJson { Table = row.Table, SourceId = row.SourceId, Code = "session-boundary-required", Message = "Vincule o movimento à sessão contada e à sequência exata deste caixa e data." });
        }
    }

    internal async Task ApplyReviewedComparison(Guid branchId, AccessSource source, ResponseGoLivePreviewJson preview, CancellationToken ct)
    {
        var groups = preview.HistoricalPlan.Where(r => r.Action == "CountedSession").GroupBy(r => (r.AccountId, r.Sequence)).ToList();
        foreach (var account in groups.GroupBy(g => g.Key.AccountId))
        {
            decimal? opening = null;
            foreach (var group in account.OrderBy(g => g.Key.Sequence))
            {
                var first = group.First();
                var date = Date(Row(source, first)["est_data"]);
                var closing = group.Sum(r => Value(r, Row(source, r)["est_valor"]));
                decimal incoming = 0, outgoing = 0;
                foreach (var row in preview.HistoricalPlan.Where(r => r.AccountId == first.AccountId && r.Sequence == first.Sequence && r.Table == "TblLancamentos"))
                {
                    var type = await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(row.TransactionTypeId!.Value, branchId, ct);
                    if (type!.Category.DefaultDirection == Direction.In) incoming += Value(row, Row(source, row)["lco_valor"]);
                    else outgoing += Value(row, Row(source, row)["lco_valor"]);
                }
                foreach (var row in preview.HistoricalPlan.Where(r => r.AccountId == first.AccountId && r.Sequence == first.Sequence && r.Action == "SessionField"))
                {
                    var value = Value(row, Row(source, row)["est_valor"]);
                    if (value > 0) incoming += value; else outgoing -= value;
                }
                var sourceAccounts = group.Select(r => Row(source, r)["con_id"]).Distinct().ToList();
                foreach (var sourceAccount in sourceAccounts)
                {
                    var day = preview.Days.SingleOrDefault(x => x.SourceAccountId == sourceAccount && x.Date == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    if (day is null) { day = new ResponseGoLiveDayJson { SourceAccountId = sourceAccount, Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }; preview.Days.Add(day); }
                    var difference = opening.HasValue ? CashVarianceFormula.Calculate(closing, opening.Value, incoming, outgoing) : (decimal?)null;
                    day.RawAppDifference = difference?.ToString(CultureInfo.InvariantCulture);
                    day.AppDifference = difference.HasValue && decimal.Abs(difference.Value) <= 999999999999.99m ? difference : null;
                    day.Completeness += opening.HasValue ? ";ReviewedMappedInputs" : ";CountedAnchorWithoutPriorOpening";
                }
                opening = closing;
            }
        }
    }

    internal static void NormalizeIntervals(RequestGoLiveResolutionJson resolution)
    {
        resolution.StartsAt = Normalize(resolution.StartsAt);
        resolution.EndsAt = Normalize(resolution.EndsAt);
    }

    private static DateTime? Normalize(DateTime? instant)
    {
        if (instant is not { } value) return null;
        if (value.Kind == DateTimeKind.Utc) return value;
        if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        if (zone.IsInvalidTime(value) || zone.IsAmbiguousTime(value)) Invalid();
        try { return TimeZoneInfo.ConvertTimeToUtc(value, zone); }
        catch (ArgumentException) { Invalid(); throw; }
    }

    private static DateTime PaidInstant(string value)
    {
        if (!DateTime.TryParseExact(value, ["yyyy/MM/dd H:mm:ss", "yyyy/MM/dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) Invalid();
        return Normalize(DateTime.SpecifyKind(local, DateTimeKind.Unspecified))!.Value;
    }

    private Task Register(BranchUser actor, GoLiveImport import, RequestGoLiveResolutionJson row, Guid target, string kind, CancellationToken ct) =>
        imports.Register(new GoLiveImportTarget
        {
            BranchId = actor.BranchId,
            GoLiveImportId = import.Id,
            SourceTable = row.Table,
            SourceId = row.SourceId,
            TargetId = target,
            TargetKind = kind
        }, ct);
    private static Dictionary<string, string> Row(AccessSource source, RequestGoLiveResolutionJson r) => source.Row(r.Table, r.SourceId);
    internal static DateTime Date(string value) => DateTime.TryParseExact(value, ["yyyy/MM/dd H:mm:ss", "yyyy/MM/dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.Date : throw new OnValidationException([ResourcesErrorMessages.GOLIVE_SOURCE_DATE_INVALID]);
    internal static decimal Value(RequestGoLiveResolutionJson resolution, string raw)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(raw, "^-?[0-9]{1,12}(?:\\.[0-9]{1,18})?$")) Invalid();
        if (resolution.TargetValue is null && raw.Contains('.') && raw.Split('.')[1].Skip(2).Any(c => c != '0'))
            throw new OnValidationException([ResourcesErrorMessages.GOLIVE_MONEY_PRECISION_REQUIRED]);
        if (!decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) Invalid();
        if (resolution.TargetValue is { } reviewed) value = reviewed;
        if (decimal.Round(value, 2) != value) throw new OnValidationException([ResourcesErrorMessages.GOLIVE_MONEY_PRECISION_REQUIRED]);
        if (decimal.Abs(value) > 999999999999.99m) Invalid();
        return value;
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
}
