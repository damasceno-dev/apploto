using System.Globalization;
using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.People;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Imports;

/// <summary>Imports use the same pay/hour/draw validators and command stages as current people records.</summary>
public class GoLivePeopleStage(
    RecordHourBankEntryUseCase hours,
    RecordPayItemUseCase pay,
    RecordSpecialDrawUseCase draws,
    IEmployeeRecordsRepository records,
    IGoLiveHistoryRepository history,
    IGoLiveImportsRepository imports,
    IOperatorsRepository operators,
    ITimeEntriesRepository attendance,
    ITimeEntryPoliciesRepository policies,
    IHolidaysRepository holidays,
    ITimeEntryCalculationService calculation,
    IBranchClock clock,
    LockDateGuard locks,
    ILockDateReader lockReader,
    IUnitOfWork unitOfWork)
{
    internal static bool Supported(RequestGoLiveResolutionJson row) =>
        row.Table == "TblRelMensal" && row.Action == "PayItem"
        || row.Table == "TblMovimentacaoBancoHoras" && row.Action is "HourOpening" or "HourPayment" or "HourAdjustment" or "AttendanceEvidence"
        || row.Table == "TblRegistroPonto" && row.Action == "Attendance"
        || row.Table == "TblLoteriaEspecial" && row.Action == "SpecialDraw";

    internal async Task<ResponseGoLiveAttendanceComparisonJson> CompareAttendance(Guid branchId, RequestGoLiveResolutionJson resolution, Dictionary<string, string> source, CancellationToken ct)
    {
        var entry = await Attendance(branchId, resolution, source, ct);
        var policy = TimeEntryPolicyResolver.Resolve(await policies.ListActiveByBranchIdAsNoTracking(branchId, ct), entry.Date);
        return new() { SourceId = resolution.SourceId, SourceTotalHours = source["pon_totalHoras"], SourceBalanceHours = source["pon_saldoHoras"], PolicyId = policy.Id, DailyTargetHours = policy.DailyTargetHours, LunchDeductionOver6H = policy.LunchDeductionOver6H, LunchDeductionOver4H = policy.LunchDeductionOver4H, CalculatedTotalHours = entry.TotalHours, CalculatedBalanceHours = entry.BalanceHours };
    }

    internal async Task ValidatePayCategory(Guid branchId, Guid id, CancellationToken ct)
    {
        if (!PeopleHistory.Heads(await records.List<EmployeePayCategory>(branchId, ct), x => x.PreviousId).Any(x => x.Id == id && x.Enabled)) Invalid();
    }

    internal async Task Validate(Guid branchId, RequestGoLiveResolutionJson resolution, Dictionary<string, string> source, CancellationToken ct)
    {
        GoLiveHistoryStage.NormalizeIntervals(resolution);
        if (!resolution.HistoryComplete || resolution.PostedOn is null || resolution.PostedOn.Value.TimeOfDay != TimeSpan.Zero
            || resolution.PostedOn.Value > clock.LocalBusinessDate(clock.UtcNow())) Invalid();
        var posted = resolution.PostedOn!.Value;
        await locks.EnsureNotLocked(branchId, posted, ResourcesErrorMessages.PEOPLE_PERIOD_LOCKED, ct);
        if (resolution.Action != "SpecialDraw" && await operators.GetByIdAndBranchIdAsNoTracking(resolution.OperatorId ?? Guid.Empty, branchId, ct) is null)
            throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        switch (resolution.Action)
        {
            case "PayItem":
                var request = PayRequest(resolution, source);
                PeopleValidation.Validate(new PayItemFluentValidation(), request);
                var categories = PeopleHistory.Heads(await records.List<EmployeePayCategory>(branchId, ct), x => x.PreviousId);
                if (!categories.Any(x => x.Id == request.CategoryId && x.Enabled && x.Kind == request.Kind)) Invalid();
                if (request.SourceDate > posted || request.SourceDate.Year != request.SourceMonth.Year || request.SourceDate.Month != request.SourceMonth.Month) Invalid();
                if (request.Kind == PayItemKind.Deduction && string.IsNullOrWhiteSpace(request.DeductionPolicy)) Invalid();
                await locks.EnsureNotLocked(branchId, request.SourceMonth, ResourcesErrorMessages.PEOPLE_PERIOD_LOCKED, ct);
                break;
            case "HourOpening":
            case "HourPayment":
            case "HourAdjustment":
                var hour = HourRequest(resolution, source, "preview");
                PeopleValidation.Validate(new HourBankEntryFluentValidation(), hour);
                if (hour.SourceDate > posted) Invalid();
                if (hour.Kind == HourBankEntryKind.Opening) await locks.EnsureNotLocked(branchId, hour.SourceDate, ResourcesErrorMessages.PEOPLE_PERIOD_LOCKED, ct);
                break;
            case "Attendance":
                _ = await Attendance(branchId, resolution, source, ct);
                break;
            case "AttendanceEvidence":
                if (source["mov_tipo"] != "Ponto" || string.IsNullOrWhiteSpace(source["pon_id"])) Invalid();
                break;
            case "SpecialDraw":
                PeopleValidation.Validate(new SpecialDrawFluentValidation(), DrawRequest(resolution, source));
                break;
            default: Invalid(); break;
        }
    }

    internal async Task<IReadOnlyList<Guid>> Stage(BranchUser actor, GoLiveImport import,
        AccessSource source, IReadOnlyList<RequestGoLiveResolutionJson> plan, CancellationToken ct)
    {
        var ids = new List<Guid>();
        var locked = await lockReader.Read(actor.BranchId, ct);
        var attendanceTargets = new Dictionary<string, Guid>();
        foreach (var resolution in plan.OrderBy(x => x.Action == "HourOpening" ? 0 : x.Action == "AttendanceEvidence" ? 2 : 1).ThenBy(x => x.SourceDate).ThenBy(x => x.SourceId, StringComparer.Ordinal))
        {
            var row = source.Row(resolution.Table, resolution.SourceId);
            await Validate(actor.BranchId, resolution, row, ct);
            Guid id;
            switch (resolution.Action)
            {
                case "PayItem": (id, _) = await pay.Stage(actor, locked, PayRequest(resolution, row), null, ct); break;
                case "HourOpening":
                case "HourPayment":
                case "HourAdjustment": (id, _) = await hours.Stage(actor, locked, HourRequest(resolution, row, import.SourceDigest), null, ct); break;
                case "SpecialDraw": (id, _) = await draws.Stage(actor, locked, DrawRequest(resolution, row), null, ct); break;
                case "Attendance":
                    var entry = await Attendance(actor.BranchId, resolution, row, ct);
                    await history.Add(entry, ct);
                    await unitOfWork.Commit(ct);
                    id = entry.Id;
                    attendanceTargets.Add(resolution.SourceId, id);
                    break;
                case "AttendanceEvidence":
                    if (!attendanceTargets.TryGetValue(row["pon_id"], out id)) Invalid();
                    break;
                default: Invalid(); throw new InvalidOperationException();
            }
            await imports.Register(new GoLiveImportTarget
            {
                BranchId = actor.BranchId,
                GoLiveImportId = import.Id,
                SourceTable = resolution.Table,
                SourceId = resolution.SourceId,
                TargetId = id,
                TargetKind = resolution.Action
            }, ct);
            ids.Add(id);
        }
        return ids;
    }

    internal async Task ValidatePlan(Guid branchId, AccessSource source, IReadOnlyList<RequestGoLiveResolutionJson> plan, CancellationToken ct)
    {
        var existing = await records.List<HourBankEntry>(branchId, ct);
        var plannedOpenings = plan.Where(x => x.Action == "HourOpening").ToLookup(x => (Guid?)x.OperatorId);
        var existingOpenings = existing.Where(x => x.Kind == HourBankEntryKind.Opening).ToLookup(x => (Guid?)x.OperatorId);
        var attendanceBySource = plan.Where(x => x.Table == "TblRegistroPonto" && x.Action == "Attendance").ToDictionary(x => x.SourceId);
        foreach (var opening in plannedOpenings.SelectMany(x => x))
            if (existingOpenings[opening.OperatorId].Any()
                || plannedOpenings[opening.OperatorId].Count() > 1) Invalid();
        foreach (var row in plan.Where(x => x.Action is "HourPayment" or "HourAdjustment"))
        {
            var date = HourRequest(row, source.Row(row.Table, row.SourceId), "preview").SourceDate;
            if (!existingOpenings[row.OperatorId].Any(x => x.SourceDate <= date && x.PostedOn <= row.PostedOn)
                && !plannedOpenings[row.OperatorId].Any(x => HourRequest(x, source.Row(x.Table, x.SourceId), "preview").SourceDate <= date && x.PostedOn <= row.PostedOn)) Invalid();
        }
        foreach (var row in plan.Where(x => x.Action == "AttendanceEvidence"))
        {
            var evidence = source.Row(row.Table, row.SourceId);
            var target = attendanceBySource.GetValueOrDefault(evidence["pon_id"]);
            if (target is null || target.OperatorId != row.OperatorId) Invalid();
        }
        if (plan.Where(x => x.Action == "Attendance").GroupBy(x => (x.OperatorId, Date: GoLiveHistoryStage.Date(source.Row(x.Table, x.SourceId)["pon_data"]))).Any(x => x.Count() > 1)) Invalid();
    }

    private async Task<TimeEntry> Attendance(Guid branchId, RequestGoLiveResolutionJson resolution, Dictionary<string, string> source, CancellationToken ct)
    {
        var date = GoLiveHistoryStage.Date(source["pon_data"]);
        await locks.EnsureNotLocked(branchId, date, ResourcesErrorMessages.PEOPLE_PERIOD_LOCKED, ct);
        if (resolution.AttendanceStatus is not { } status || !Enum.IsDefined(status) || date > resolution.PostedOn
            || await attendance.ExistsActiveByBranchIdOperatorIdAndDate(branchId, resolution.OperatorId!.Value, date)) Invalid();
        status = resolution.AttendanceStatus!.Value;
        await TimeEntryRules.EnsureCalendar(status, date, branchId, holidays);
        var entry = new TimeEntry { BranchId = branchId, OperatorId = resolution.OperatorId!.Value, Date = date, Status = resolution.AttendanceStatus!.Value };
        var segments = new List<TimeEntrySegmentInput>();
        if (status == TimeEntryStatus.Present)
        {
            if (resolution.StartsAt is not { Kind: DateTimeKind.Utc } start || resolution.EndsAt is not { Kind: DateTimeKind.Utc } end
                || end <= start || end > clock.UtcNow() || clock.LocalBusinessDate(start) != date) Invalid();
            var localStart = clock.LocalBusinessDateTime(resolution.StartsAt!.Value);
            var localEnd = clock.LocalBusinessDateTime(resolution.EndsAt!.Value);
            segments.Add(new(localStart, localEnd));
            entry.Segments.Add(new TimeEntrySegment { TimeEntryId = entry.Id, ClockIn = localStart, ClockOut = localEnd });
        }
        TimeEntryRules.EnsureSegments(entry);
        var policy = TimeEntryPolicyResolver.Resolve(await policies.ListActiveByBranchIdAsNoTracking(branchId, ct), date);
        var result = calculation.Calculate(status, segments, date, clock.LocalBusinessDateTime(clock.UtcNow()), policy.DailyTargetHours, policy.LunchDeductionOver6H, policy.LunchDeductionOver4H);
        entry.TotalHours = result.TotalHours; entry.BalanceHours = result.BalanceHours;
        return entry;
    }

    private static RequestPayItemJson PayRequest(RequestGoLiveResolutionJson r, Dictionary<string, string> source)
    {
        var month = 0;
        if (r.Year is null || r.SourceDate is null || r.PayKind is null || !int.TryParse(source["rel_data"], NumberStyles.None, CultureInfo.InvariantCulture, out month) || month is < 1 or > 12) Invalid();
        return new RequestPayItemJson
        {
            OperatorId = r.OperatorId!.Value,
            CategoryId = r.PayCategoryId ?? Guid.Empty,
            Kind = r.PayKind.Value,
            SourceMonth = new DateTime(r.Year.Value, month, 1),
            SourceDate = r.SourceDate.Value,
            PostedOn = r.PostedOn!.Value,
            Amount = GoLiveHistoryStage.Value(r, source["rel_valor"]),
            Reason = r.Reason,
            Evidence = r.Reason,
            DeductionPolicy = r.DeductionPolicy
        };
    }

    private static RequestHourBankEntryJson HourRequest(RequestGoLiveResolutionJson r, Dictionary<string, string> source, string digest)
    {
        if (r.Action == "HourPayment" && source["mov_tipo"] != "Pagamento" || r.Action == "HourAdjustment" && source["mov_tipo"] != "Ajuste") Invalid();
        if (!decimal.TryParse(source["mov_minutos"], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var minutes)) Invalid();
        var target = r.TargetHours ?? minutes / 60m;
        return new RequestHourBankEntryJson
        {
            OperatorId = r.OperatorId!.Value,
            Kind = r.Action switch { "HourOpening" => HourBankEntryKind.Opening, "HourPayment" => HourBankEntryKind.Payment, _ => HourBankEntryKind.Adjustment },
            SourceDate = r.SourceDate ?? GoLiveHistoryStage.Date(source["mov_data"]),
            SignedHours = target,
            PostedOn = r.PostedOn!.Value,
            ExternalIdentity = "access:" + digest[..Math.Min(digest.Length, 16)] + ":" + r.SourceId,
            Reason = r.Reason,
            Evidence = r.Reason
        };
    }

    private static RequestSpecialDrawJson DrawRequest(RequestGoLiveResolutionJson r, Dictionary<string, string> source) => new()
    {
        PostedOn = r.PostedOn!.Value,
        Reason = r.Reason,
        Evidence = r.Reason,
        Name = r.TargetName ?? source["lot_nome"],
        Modality = source["lot_modalidade"],
        Contest = source["lot_concurso"],
        PeriodFrom = GoLiveHistoryStage.Date(source["lot_data_inicio"]),
        PeriodTo = GoLiveHistoryStage.Date(source["lot_data_fim"]),
        Prize = GoLiveHistoryStage.Value(r, source["lot_premio"])
    };

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new OnValidationException([ResourcesErrorMessages.GOLIVE_IMPORT_UNRESOLVED]);
}
