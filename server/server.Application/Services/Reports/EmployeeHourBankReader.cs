using server.Application.UseCases.People;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.Services.Reports;

public class EmployeeHourBankReader(IEmployeeRecordsRepository records, TimeEntryBalanceProjector attendance)
{
    public Task<IReadOnlyList<HourBankEntry>> LoadHistory(Guid branchId, CancellationToken ct) => records.List<HourBankEntry>(branchId, ct);

    public async Task<IReadOnlyList<Guid>> PeopleWithHistory(Guid branchId, DateTime cutoff, CancellationToken ct) =>
        (await records.List<HourBankEntry>(branchId, ct)).Where(r => r.PostedOn <= cutoff).Select(r => r.OperatorId).Distinct().ToList();

    public async Task<ResponseHourBankJson> Read(
        Guid branchId,
        Operator person,
        DateTime from,
        DateTime to,
        DateTime cutoff,
        DateTime localNow,
        int page,
        int pageSize,
        CancellationToken ct,
        ResponseTimeEntryBalanceOperatorJson? periodAttendance = null,
        IReadOnlyList<HourBankEntry>? branchEntries = null)
    {
        to = to < cutoff ? to : cutoff;
        var all = (branchEntries ?? await records.List<HourBankEntry>(branchId, ct)).Where(r => r.OperatorId == person.Id && r.PostedOn <= cutoff && r.PostedOn <= to).ToList();
        var opening = all.SingleOrDefault(r => r.Kind == HourBankEntryKind.Opening && r.PreviousId == null && r.SourceDate <= to);
        var result = new ResponseHourBankJson
        {
            OperatorId = person.Id,
            DateFrom = from,
            DateTo = to,
            AsOfDate = cutoff,
            OpeningSourceId = opening?.Id,
            OpeningSourceDate = opening?.SourceDate,
            Completeness = opening == null ? PeopleSourceCompleteness.NotConfigured : PeopleSourceCompleteness.Complete,
            MissingInput = opening == null ? "Gestor: declarar saldo inicial, data de início e evidência. Histórico anterior não é presumido como zero." : string.Empty,
            EntryCount = all.Count,
            Entries = all.OrderBy(r => r.PostedOn).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.ToResponse()).ToList()
        };
        var start = opening?.SourceDate ?? from;
        var projection = start >= from && periodAttendance != null
            ? periodAttendance
            : await attendance.BuildForOperator(branchId, person, start, to, localNow, ct);
        var days = projection.Days.Where(d => d.Date >= start && d.Date <= to).ToList();
        if (days.Count > 10000 || all.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        result.AttendanceDays = days;
        result.ContainsInProgress = days.Any(d => d.IsInProgress);
        result.AttendanceContributionHours = days.Where(d => d.Date >= from).Sum(d => d.BalanceHours);
        result.PaymentHours = all.Where(r => r.Kind == HourBankEntryKind.Payment && r.PostedOn >= from).Sum(r => r.SignedContribution);
        result.AdjustmentHours = all.Where(r => r.Kind == HourBankEntryKind.Adjustment && r.PostedOn >= from).Sum(r => r.SignedContribution);
        result.PeriodOpeningHours = all.Where(r => r.Kind == HourBankEntryKind.Opening && r.PostedOn >= from &&
            (r.PreviousId != null || r.SourceDate >= from)).Sum(r => r.SignedContribution);
        if (opening != null)
        {
            // An evidenced anchor applies at SourceDate, and becomes available only at PostedOn. If declared
            // later, reconstruct this window's carry-over from that anchor without crediting it again as a flow.
            // Earlier windows still exclude it by PostedOn, so their unknown opening is never rewritten.
            result.CarryOverHours = opening.SourceDate < from
                ? days.Where(d => d.Date < from).Sum(d => d.BalanceHours) + all.Where(r => r.PostedOn < from).Sum(r => r.SignedContribution)
                    + (opening.PostedOn >= from ? opening.SignedContribution : 0m)
                : null;
            result.ClosingBalanceHours = days.Sum(d => d.BalanceHours) + all.Sum(r => r.SignedContribution);
            if (result.ContainsInProgress) result.Completeness = PeopleSourceCompleteness.Incomplete;
        }
        return result;
    }
}
