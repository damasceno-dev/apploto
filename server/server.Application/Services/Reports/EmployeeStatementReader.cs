using server.Application.Services.Transactions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.UseCases.People;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.Services.Reports;

public class EmployeeStatementReader(
    IEmployeeRecordsRepository records,
    IEmployeeStatementSources sources,
    IOperatorsRepository operators,
    PersonalDifferencesReader differences,
    IPersonalDifferencesRepository personalSources,
    ICashVarianceProductResolver varianceProduct,
    TimeEntryBalanceProjector attendance,
    EmployeeHourBankReader hours,
    EmployeeSpecialDrawReader specialDraws,
    ILockDateReader lockDate,
    IBranchClock clock)
{
    public async Task<ResponseEmployeeStatementJson> ReadForCommand(
        Guid branchId,
        Guid operatorId,
        int year,
        int month,
        DateTime cutoff,
        CancellationToken ct) =>
        await Read(branchId, operatorId, year, month, cutoff, 1, 10000, ct);

    public async Task<ResponseEmployeeStatementJson> Read(
        Guid branchId,
        Guid operatorId,
        int year,
        int month,
        DateTime cutoff,
        int page,
        int pageSize,
        CancellationToken ct,
        DateTime? capturedLocalNow = null)
    {
        var person = await operators.GetByIdAndBranchIdAsNoTracking(operatorId, branchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        var (from, to, _) = MonthlyFinancialReader.MonthWindow(year, month, cutoff);
        var unresolvedCustody = await sources.UnresolvedCustodySessions(branchId, from, to, ct);
        var sessions = await sources.Sessions(branchId, operatorId, from, to, ct);
        var basis = await sources.CommissionBasis(branchId, operatorId, from, to, ct);
        if (unresolvedCustody.Count > 10000 || sessions.Count > 10000 || basis.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var allPay = await records.List<EmployeePayItem>(branchId, ct);
        var pay = allPay.Where(r => r.OperatorId == operatorId && r.PostedOn >= from && r.PostedOn <= to).ToList();
        if (pay.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var product = await varianceProduct.GetIdAsync(branchId, ct);
        var branchPersonal = await personalSources.Read(branchId, product, from, to, null, 1, 1, ct);
        var localNow = capturedLocalNow ?? clock.LocalBusinessDateTime(clock.UtcNow());
        var applyCurrentThreshold = year == localNow.Year && month == localNow.Month && cutoff == localNow.Date;
        var difference = await differences.Read(branchId, product, year, month, cutoff, operatorId, page, pageSize, applyCurrentThreshold, ct);
        if (difference.TotalCount > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var periodAttendance = await attendance.BuildForOperator(branchId, person, from, to, localNow, ct);
        var statement = new ResponseEmployeeStatementJson
        {
            OperatorId = person.Id,
            OperatorName = person.Name,
            OperatorActive = person.Active,
            DateFrom = from,
            DateTo = to,
            AsOfDate = cutoff,
            IsLockedPeriod = to <= await lockDate.Read(branchId, ct),
            Page = page,
            PageSize = pageSize,
            AutomaticPayrollDeduction = false,
            RepresentsPayment = false,
            SessionCount = sessions.Count,
            CustodyHistoryComplete = unresolvedCustody.Count == 0 && sessions.All(s => s.HistoryComplete),
            UnresolvedCustodySessionIds = unresolvedCustody.Select(s => s.SessionId).ToList(),
            UnassignedInformationalSourceCount = branchPersonal.Totals.Where(t => t.OperatorId == null).Sum(t => t.Count),
            RecordedPaySignedTotal = pay.Sum(r => r.SignedContribution),
            RecordedAdditions = pay.Where(r => r.Kind == PayItemKind.Addition).Sum(r => r.SignedContribution),
            RecordedDeductions = -pay.Where(r => r.Kind == PayItemKind.Deduction).Sum(r => r.SignedContribution),
            PayItemCount = pay.Count,
            PayItems = pay.OrderBy(r => r.PostedOn).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.ToResponse()).ToList(),
            Sessions = sessions.Skip((page - 1) * pageSize).Take(pageSize).Select(s => new ResponseEmployeeSessionJson
            {
                SessionId = s.SessionId,
                AccountId = s.AccountId,
                CustodianOperatorId = s.CustodianOperatorId,
                RecorderOperatorId = s.RecorderOperatorId,
                SubmitterOperatorId = s.SubmitterOperatorId,
                OfficialAttemptId = s.OfficialAttemptId,
                SourceDate = s.SourceDate,
                Status = s.Status,
                HistoryComplete = s.HistoryComplete
            }).ToList(),
            Differences = difference,
            SpecialDraws = await specialDraws.Read(branchId, person.Id, from, to, cutoff, ct),
            Attendance = periodAttendance,
            HourBank = await hours.Read(branchId, person, from, to, cutoff, localNow, page, pageSize, ct, periodAttendance),
            Commission = EmployeeCommissionCalculator.Calculate(operatorId, from, to, cutoff,
                await records.List<EmployeeCommissionRule>(branchId, ct), basis, await records.List<EmployeeCommissionSource>(branchId, ct), page, pageSize),
            Limitations = ["Demonstrativo gerencial; não é comprovante de pagamento nem recibo legal de salário.",
                "Comissão calculada é proposta informativa e não integra o total de itens registrados.",
                "Prêmios e sobras legados dependem de origem e atribuição explícitas pelo gestor; nomes de produtos não determinam a base.",
                "Corte por data de publicação, não por instante de auditoria; períodos abertos admitem correções autorizadas.",
                "Origens sem atribuição são informação da filial, sem responsabilidade pessoal. Dados privados do Access não foram reproduzidos."]
        };
        if (!statement.CustodyHistoryComplete)
        {
            statement.Commission.Completeness = PeopleSourceCompleteness.Incomplete;
            statement.Commission.ProposedAmount = null;
            statement.Commission.CalculationId = null;
            statement.Commission.MissingInputs.Add("Gestor: revisar a proveniência de custódia das sessões; histórico incompleto não permite comissão comprovada.");
        }
        var classificationStates = await sources.CommissionClassificationStates(branchId, operatorId, from, to, ct);
        if (classificationStates.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var divergent = classificationStates.Where(s => !s.MatchesPublishedBasis && clock.LocalBusinessDate(s.ClassifiedAt) <= cutoff &&
            (s.SourceDate >= from && s.SourceDate <= to || clock.LocalBusinessDate(s.ClassifiedAt) >= from && clock.LocalBusinessDate(s.ClassifiedAt) <= to)).ToList();
        if (divergent.Count > 0)
        {
            statement.Commission.Completeness = PeopleSourceCompleteness.Incomplete;
            statement.Commission.ProposedAmount = null;
            statement.Commission.CalculationId = null;
            foreach (var change in divergent)
                statement.Commission.MissingInputs.Add($"Gestor: a classificação atual da transação {change.TransactionId} diverge da base publicada. Ressubmeter a sessão {change.SessionId} após conferência; a comissão mantém a classificação da última submissão oficial até essa publicação.");
        }
        // Reconciliation belongs to the original source month; signed pay deltas belong to their posting month.
        var recordedCommission = PeopleHistory.Heads(allPay.Where(r => r.OperatorId == operatorId && r.SourceMonth == from &&
            r.PostedOn <= cutoff && r.CommissionCalculationId != null), r => r.PreviousId).SingleOrDefault();
        statement.Commission.RecordedPayItemId = recordedCommission?.Id;
        statement.Commission.RecordedCalculationId = recordedCommission?.CommissionCalculationId;
        statement.Commission.RecordedSourceMonthAmount = recordedCommission == null || recordedCommission.Cancelled ? 0 : recordedCommission.Amount;
        statement.Commission.DifferenceFromRecorded = statement.Commission.ProposedAmount - statement.Commission.RecordedSourceMonthAmount;
        statement.Commission.RequiresReconciliation = recordedCommission != null && statement.Commission.DifferenceFromRecorded != 0;
        return statement;
    }
}
