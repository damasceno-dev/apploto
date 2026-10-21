using server.Application.Services.Members;
using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.TimeEntryBalance;

public class GetTimeEntryBalanceSummaryUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    TimeEntryBalanceProjector timeEntryBalanceProjector,
    IBranchClock branchClock,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseTimeEntryBalanceSummaryJson> Execute(
        RequestTimeEntryBalanceSummaryJson request,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        // Resolve the read into one of three shapes: a single named/own operator,
        // an empty result (unlinked Member), or the branch-wide roll-up
        // (Manager/Admin who named neither an operator nor Mine).
        Operator? targetOperator = null;
        var branchWide = false;

        if (branchUser.Role is Role.Member)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

            // Empty-scope short-circuit: a Member with no linked operator has nothing
            // to summarize, regardless of Mine or OperatorId. A Member never reaches
            // the branch-wide path.
            if (memberScope.LinkedOperator is null)
                return EmptyResponse(request);

            // Members cannot read another operator's balance. Mine = true, an omitted
            // OperatorId, and an explicit own OperatorId all resolve to the linked operator.
            if (request.OperatorId is { } explicitOperatorId && explicitOperatorId != memberScope.LinkedOperator.Id)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);

            targetOperator = memberScope.LinkedOperator;
        }
        else if (request.Mine || request.OperatorId is not null)
        {
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

            // M11 Phase 2.4: an explicitly named operator may be deactivated (its history stays drillable); Mine
            // keeps resolving the caller's own active linked operator. Missing or cross-branch → 404.
            targetOperator = (request.Mine
                    ? await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(targetOperatorId, branchUser.BranchId, ct)
                    : await operatorsRepository.GetByIdAndBranchIdAsNoTracking(targetOperatorId, branchUser.BranchId, ct))
                ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        }
        else
        {
            // Manager/Admin who named neither an operator nor Mine → branch-wide roll-up.
            branchWide = true;
        }

        // Capture the branch-local clock once for the whole report (NOT per row, NOT per
        // operator) so every operator's live-running recompute shares one consistent "now".
        var branchLocalNow = branchClock.LocalBusinessDateTime(branchClock.UtcNow());

        // M11 Phase 6: the shared projector is the one computation behind this report, the monthly financial report
        // and the management overview.
        var operators = branchWide
            ? await timeEntryBalanceProjector.BuildBranchWide(branchUser.BranchId, request.DateFrom, request.DateTo, branchLocalNow, ct)
            : [await timeEntryBalanceProjector.BuildForOperator(branchUser.BranchId, targetOperator!, request.DateFrom, request.DateTo, branchLocalNow, ct)];

        return new ResponseTimeEntryBalanceSummaryJson
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Operators = operators
        };
    }

    private static ResponseTimeEntryBalanceSummaryJson EmptyResponse(RequestTimeEntryBalanceSummaryJson request)
    {
        return new ResponseTimeEntryBalanceSummaryJson
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Operators = []
        };
    }

    private static void Validate(RequestTimeEntryBalanceSummaryJson request)
    {
        var result = new TimeEntryBalanceSummaryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
