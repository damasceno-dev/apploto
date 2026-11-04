using server.Application.UseCases.Cheques;
using server.Application.UseCases.Cheques.ConvertToFiado;
using server.Application.UseCases.Cheques.Deposit;
using server.Application.UseCases.Cheques.Return;
using server.Application.UseCases.TransactionTypes.Reclassify;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Cheques;

/// <summary>M9.7 request shape: dates in range and ordered, distinct bounded items, required client and Tab, bounded text.</summary>
public class ChequeCommandFluentValidationTest
{
    private static readonly DateTime Day = new(2025, 10, 2);

    private static RequestChequeDepositJson Deposit(Action<List<RequestChequeOperationItemJson>>? items = null, DateTime? occurredOn = null,
        DateTime? postedOn = null)
    {
        var list = new List<RequestChequeOperationItemJson> { new() { TransactionId = Guid.NewGuid(), ExpectedVersion = 0 } };
        items?.Invoke(list);
        return new RequestChequeDepositJson { BankAccountId = Guid.NewGuid(), OccurredOn = occurredOn ?? Day, PostedOn = postedOn, Items = list };
    }

    private static IReadOnlyList<string> Errors(RequestChequeDepositJson request) =>
        new ChequeDepositFluentValidation().Validate(request).Errors.Select(e => e.ErrorMessage).ToList();

    [Fact]
    public void Deposit_ShouldAcceptAValidBatch() => Errors(Deposit()).ShouldBeEmpty();

    [Fact]
    public void Deposit_ShouldRefuseAnEmptyBatch() =>
        Errors(Deposit(items => items.Clear())).ShouldContain(ResourcesErrorMessages.CHEQUE_ITEMS_REQUIRED);

    [Fact]
    public void Deposit_ShouldRefuseTheSameChequeTwice() =>
        Errors(Deposit(items => items.Add(new RequestChequeOperationItemJson { TransactionId = items[0].TransactionId })))
            .ShouldContain(ResourcesErrorMessages.CHEQUE_ITEMS_DUPLICATE);

    [Fact]
    public void Deposit_ShouldRefuseMoreThanTheItemCap() =>
        Errors(Deposit(items => items.AddRange(Enumerable.Range(0, ChequeValidationExtensions.MaxItems)
                .Select(_ => new RequestChequeOperationItemJson { TransactionId = Guid.NewGuid() }))))
            .ShouldContain(string.Format(ResourcesErrorMessages.CHEQUE_ITEMS_MAX_COUNT, ChequeValidationExtensions.MaxItems));

    [Fact]
    public void Deposit_ShouldRefuseANegativeExpectedVersion() =>
        Errors(Deposit(items => items[0] = new RequestChequeOperationItemJson { TransactionId = items[0].TransactionId, ExpectedVersion = -1 }))
            .ShouldContain(ResourcesErrorMessages.CHEQUE_EXPECTED_VERSION_INVALID);

    [Fact]
    public void Deposit_ShouldRequireTheOccurrenceDate() =>
        Errors(Deposit(occurredOn: default(DateTime))).ShouldContain(ResourcesErrorMessages.SETTLEMENT_OCCURRED_ON_REQUIRED);

    [Fact]
    public void Deposit_ShouldRefuseADateOutOfRange() =>
        Errors(Deposit(occurredOn: new DateTime(1999, 12, 31))).ShouldContain(ResourcesErrorMessages.SETTLEMENT_DATE_OUT_OF_RANGE);

    [Fact]
    public void Deposit_ShouldRefuseAPostingBeforeTheOccurrence() =>
        Errors(Deposit(postedOn: Day.AddDays(-1))).ShouldContain(ResourcesErrorMessages.SETTLEMENT_POSTED_ON_BEFORE_OCCURRED_ON);

    [Fact]
    public void Return_ShouldRequireClientDepositAndALegalNextDepositDate()
    {
        var valid = new RequestChequeReturnJson
        {
            DepositEventId = Guid.NewGuid(), ClientId = Guid.NewGuid(), OccurredOn = Day, NextExpectedDepositDate = Day.AddDays(3)
        };
        var validator = new ChequeReturnFluentValidation();
        validator.Validate(valid).IsValid.ShouldBeTrue();

        IEnumerable<string> ErrorsOf(RequestChequeReturnJson request) => validator.Validate(request).Errors.Select(e => e.ErrorMessage);
        ErrorsOf(new RequestChequeReturnJson { DepositEventId = Guid.NewGuid(), OccurredOn = Day, NextExpectedDepositDate = Day })
            .ShouldContain(ResourcesErrorMessages.CHEQUE_CLIENT_REQUIRED);
        ErrorsOf(new RequestChequeReturnJson { ClientId = Guid.NewGuid(), OccurredOn = Day, NextExpectedDepositDate = Day })
            .ShouldContain(ResourcesErrorMessages.CHEQUE_DEPOSIT_EVENT_REQUIRED);
        ErrorsOf(new RequestChequeReturnJson
            {
                DepositEventId = Guid.NewGuid(), ClientId = Guid.NewGuid(), OccurredOn = Day, NextExpectedDepositDate = Day.AddDays(-1)
            })
            .ShouldContain(string.Format(ResourcesErrorMessages.CHEQUE_NEXT_DEPOSIT_DATE_INVALID, ChequeValidationExtensions.NextDepositMaxDays));
        ErrorsOf(new RequestChequeReturnJson
            {
                DepositEventId = Guid.NewGuid(), ClientId = Guid.NewGuid(), OccurredOn = Day, NextExpectedDepositDate = Day,
                ReturnReason = new string('x', ChequeValidationExtensions.ReturnReasonMaxLength + 1)
            })
            .ShouldContain(string.Format(ResourcesErrorMessages.CHEQUE_RETURN_REASON_MAX_LENGTH, ChequeValidationExtensions.ReturnReasonMaxLength));
    }

    [Fact]
    public void Convert_ShouldRequireTheIdentifiedClientAndTheTab()
    {
        var errors = new ChequeConvertToFiadoFluentValidation()
            .Validate(new RequestChequeConvertToFiadoJson { OccurredOn = Day })
            .Errors.Select(e => e.ErrorMessage).ToList();
        errors.ShouldContain(ResourcesErrorMessages.CHEQUE_CLIENT_REQUIRED);
        errors.ShouldContain(ResourcesErrorMessages.CHEQUE_FIADO_TAB_ACCOUNT_REQUIRED);
    }

    [Fact]
    public void Reclassify_ShouldRequireAWindowReasonAndDistinctVersionedRows()
    {
        var validator = new ReclassifyTransactionTypeFluentValidation();
        var row = new RequestReclassifyRowJson { TransactionId = Guid.NewGuid(), Version = 3 };
        validator.Validate(new RequestReclassifyTransactionTypeJson
            { DateFrom = Day, DateTo = Day, Reason = "ok", Rows = [row] }).IsValid.ShouldBeTrue();

        IEnumerable<string> ErrorsOf(RequestReclassifyTransactionTypeJson request) =>
            validator.Validate(request).Errors.Select(e => e.ErrorMessage);
        ErrorsOf(new RequestReclassifyTransactionTypeJson { DateFrom = Day, DateTo = Day.AddDays(-1), Reason = "ok", Rows = [row] })
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_WINDOW_INVALID);
        ErrorsOf(new RequestReclassifyTransactionTypeJson { DateFrom = Day, DateTo = Day, Reason = "", Rows = [row] })
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_REASON_REQUIRED);
        ErrorsOf(new RequestReclassifyTransactionTypeJson { DateFrom = Day, DateTo = Day, Reason = "ok", Rows = [] })
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_ROWS_REQUIRED);
        ErrorsOf(new RequestReclassifyTransactionTypeJson { DateFrom = Day, DateTo = Day, Reason = "ok", Rows = [row, row] })
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_ROWS_INVALID);
    }
}
