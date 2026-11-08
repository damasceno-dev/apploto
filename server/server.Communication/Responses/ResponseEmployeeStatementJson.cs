using server.Domain.Entities.Enums;
namespace server.Communication.Responses;

public class ResponseEmployeeStatementJson
{
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public bool OperatorActive { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public DateTime AsOfDate { get; set; }
    public bool IsLockedPeriod { get; set; }
    public int ContractRevision { get; set; } = 1;
    public bool AutomaticPayrollDeduction { get; set; }
    public bool RepresentsPayment { get; set; }
    public decimal RecordedPaySignedTotal { get; set; }
    public decimal RecordedAdditions { get; set; }
    public decimal RecordedDeductions { get; set; }
    public int PayItemCount { get; set; }
    public int SessionCount { get; set; }
    public bool CustodyHistoryComplete { get; set; }
    public List<Guid> UnresolvedCustodySessionIds { get; set; } = [];
    public int UnassignedInformationalSourceCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public ResponsePersonalDifferencesJson Differences { get; set; } = new();
    public ResponseTimeEntryBalanceOperatorJson Attendance { get; set; } = new();
    public ResponseHourBankJson HourBank { get; set; } = new();
    public ResponseSpecialDrawTotalsJson SpecialDraws { get; set; } = new();
    public ResponseEmployeeCommissionJson Commission { get; set; } = new();
    public List<ResponseEmployeeSessionJson> Sessions { get; set; } = [];
    public List<ResponsePayItemJson> PayItems { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public class ResponseEmployeeSessionJson
{
    public Guid SessionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CustodianOperatorId { get; set; }
    public Guid? RecorderOperatorId { get; set; }
    public Guid? SubmitterOperatorId { get; set; }
    public Guid? OfficialAttemptId { get; set; }
    public DateTime SourceDate { get; set; }
    public DailyCloseStatus Status { get; set; }
    public bool HistoryComplete { get; set; }
}

public class ResponseEmployeeCommissionJson
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public DateTime AsOfDate { get; set; }

    public PeopleSourceCompleteness Completeness { get; set; }
    public decimal? ProposedAmount { get; set; }
    public string? CalculationId { get; set; }
    public Guid? RecordedPayItemId { get; set; }
    public string? RecordedCalculationId { get; set; }
    public decimal RecordedSourceMonthAmount { get; set; }
    public decimal? DifferenceFromRecorded { get; set; }
    public bool RequiresReconciliation { get; set; }
    public decimal KnownBasis { get; set; }
    public bool IsCalculatedProposal { get; set; } = true;
    public bool RepresentsPayment { get; set; }
    public int BasisSourceCount { get; set; }
    public List<ResponseCommissionRuleJson> Rules { get; set; } = [];
    public List<ResponseCommissionBasisJson> Basis { get; set; } = [];
    public List<string> MissingInputs { get; set; } = [];
}

public class ResponseCommissionBasisJson
{
    public bool PostingHistoryComplete { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid SourceId { get; set; }
    public Guid? SessionId { get; set; }
    public DateTime SourceDate { get; set; }
    public DateTime PostedOn { get; set; }
    public CommissionBasisComponent Component { get; set; }
    public decimal SignedAmount { get; set; }
    public Guid? RuleId { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal? UnroundedCommission { get; set; }
    public ClassificationProvenance? ClassificationProvenance { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public class ResponseHourBankJson
{
    public Guid OperatorId { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public DateTime AsOfDate { get; set; }
    public PeopleSourceCompleteness Completeness { get; set; }
    public Guid? OpeningSourceId { get; set; }
    public DateTime? OpeningSourceDate { get; set; }
    public decimal? CarryOverHours { get; set; }
    public decimal PeriodOpeningHours { get; set; }
    public decimal AttendanceContributionHours { get; set; }
    public decimal PaymentHours { get; set; }
    public decimal AdjustmentHours { get; set; }
    public decimal? ClosingBalanceHours { get; set; }
    public bool ContainsInProgress { get; set; }
    public int EntryCount { get; set; }
    public List<ResponseHourBankEntryJson> Entries { get; set; } = [];
    public List<ResponseTimeEntryBalanceSummaryDayJson> AttendanceDays { get; set; } = [];
    public string MissingInput { get; set; } = string.Empty;
}

public class ResponseSpecialDrawTotalsJson
{
    public DateTime AsOfDate { get; set; }
    public decimal SourceFeeTotal { get; set; }
    public decimal UnmappedSourceFeeTotal { get; set; }
    public decimal AllocatedFeeTotal { get; set; }
    public decimal UnallocatedFeeTotal { get; set; }
    public bool SourceMappingComplete { get; set; }
    public List<Guid> UnmappedSourceIds { get; set; } = [];
    public List<ResponseSpecialDrawJson> History { get; set; } = [];
    public List<ResponseSpecialDrawTotalJson> Draws { get; set; } = [];
    public List<ResponseSpecialDrawAllocationJson> Allocations { get; set; } = [];
}

public class ResponseSpecialDrawTotalJson
{
    public Guid DrawId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal AllocatedFee { get; set; }
}
