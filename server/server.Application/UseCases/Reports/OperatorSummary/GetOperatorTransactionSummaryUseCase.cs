using server.Application.Services.Members;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.OperatorSummary;

public class GetOperatorTransactionSummaryUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseOperatorTransactionSummaryJson> Execute(RequestOperatorTransactionSummaryJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        Operator targetOperator;
        IReadOnlyList<Guid>? allowedAccountIds = null;

        if (branchUser.Role is Role.Member)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

            // Empty-scope short-circuit (M5 Phase 4 list precedent): a Member with no
            // linked operator has nothing to summarize, regardless of Mine or OperatorId.
            if (memberScope.LinkedOperator is null)
                return EmptyResponse(request);

            // Members cannot read another operator's summary. Mine = true, an omitted
            // OperatorId, and an explicit own OperatorId all resolve to the linked operator.
            if (request.OperatorId is { } explicitOperatorId && explicitOperatorId != memberScope.LinkedOperator.Id)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);

            // A linked Member with zero active account links sees nothing — same
            // empty short-circuit, no repository call.
            if (memberScope.AllowedAccountIds.Count == 0)
                return EmptyResponse(request);

            targetOperator = memberScope.LinkedOperator;
            allowedAccountIds = memberScope.AllowedAccountIds;
        }
        else
        {
            // Manager/Admin have no implicit fallback: they must name an operator or
            // ask for their own via Mine. Members are exempt — an omitted OperatorId
            // resolves to their linked operator above.
            if (request.Mine is false && request.OperatorId is null)
                throw new OnValidationException([ResourcesErrorMessages.REPORT_OPERATOR_ID_REQUIRED]);

            Guid targetOperatorId;
            if (request.Mine)
            {
                var ownScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
                targetOperatorId = ownScope.LinkedOperator?.Id
                    ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
            }
            else
            {
                targetOperatorId = request.OperatorId!.Value;
            }

            targetOperator = await operatorsRepository
                    .GetActiveByIdAndBranchIdAsNoTracking(targetOperatorId, branchUser.BranchId)
                ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        }

        var projection = await transactionsRepository.SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            branchUser.BranchId, targetOperator.Id, request.DateFrom, request.DateTo, allowedAccountIds);

        return BuildResponse(request, targetOperator, projection);
    }

    private static ResponseOperatorTransactionSummaryJson BuildResponse(
        RequestOperatorTransactionSummaryJson request,
        Operator targetOperator,
        OperatorTransactionSummaryProjection projection)
    {
        return new ResponseOperatorTransactionSummaryJson
        {
            OperatorId = targetOperator.Id,
            OperatorName = targetOperator.Name,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            TotalTransactionCount = projection.TotalCount,
            TotalInValue = projection.TotalIn,
            TotalOutValue = projection.TotalOut,
            NetValue = projection.TotalIn - projection.TotalOut,
            ByCategory = projection.ByCategory
                .Select(c => new ResponseOperatorCategoryTotalJson
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    Count = c.Count,
                    TotalIn = c.TotalIn,
                    TotalOut = c.TotalOut
                })
                .ToList()
        };
    }

    private static ResponseOperatorTransactionSummaryJson EmptyResponse(RequestOperatorTransactionSummaryJson request)
    {
        return new ResponseOperatorTransactionSummaryJson
        {
            OperatorId = Guid.Empty,
            OperatorName = string.Empty,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            TotalTransactionCount = 0,
            TotalInValue = 0m,
            TotalOutValue = 0m,
            NetValue = 0m,
            ByCategory = []
        };
    }

    private static void Validate(RequestOperatorTransactionSummaryJson request)
    {
        var result = new OperatorTransactionSummaryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
