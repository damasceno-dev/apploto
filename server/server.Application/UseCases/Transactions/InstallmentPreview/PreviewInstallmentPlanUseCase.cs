using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.InstallmentPreview;

public class PreviewInstallmentPlanUseCase(
    TransactionCreatePreamble preamble,
    InstallmentPlanBuilder installmentPlanBuilder)
{
    public async Task<ResponseInstallmentPreviewJson> Execute(RequestCreateTransactionInstallmentJson request)
    {
        Validate(request);
        
        var ctx = await preamble.Resolve(request);

        if (ctx.TransactionType.SettlementRule != SettlementRule.OperatorEnteredCheque)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);

        var installmentPlan = installmentPlanBuilder.Build(request, ctx.BranchUser.BranchId, Guid.NewGuid(), ctx.BranchHolidays);

        return new ResponseInstallmentPreviewJson
        {
            TotalValue = installmentPlan.Sum(r => r.Value),
            InstallmentCount = installmentPlan.Count,
            Rows = installmentPlan.Select((row, i) => new ResponseInstallmentPreviewRowJson
            {
                Index = i + 1,
                DueDate = row.DueDate,
                Value = row.Value,
                Description = row.Description
            }).ToList()
        };
    }

    private static void Validate(RequestCreateTransactionInstallmentJson request)
    {
        var result = new CreateTransactionInstallmentFluentValidation().Validate(request);
        if (!result.IsValid)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
