using server.Application.Services.Members;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.History;

/// <summary>
/// Reads a close's review events under the close's own read permission (same rules as
/// <c>GET /dailyclose/{id}</c>), so a Member who may read the close also reads the actor names
/// without calling manager-only membership APIs. Read-only: no unit of work.
/// </summary>
public class GetDailyCloseHistoryUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseReviewEventsRepository reviewEventsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseDailyCloseHistoryJson> Execute(Guid dailyCloseId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var dailyClose = await dailyClosesRepository
            .GetByIdAndBranchIdAsNoTracking(dailyCloseId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        if (branchUser.Role is Role.Member)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);

            if (memberScope.LinkedOperator is null)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);

            if (memberScope.AllowedAccountIds.Contains(dailyClose.AccountId) is false)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        var events = await reviewEventsRepository.ListByDailyCloseIdAndBranchIdAsNoTracking(
            dailyClose.Id,
            branchUser.BranchId,
            ct);
        var currentOpeningSource = await dailyClosesRepository.GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
            branchUser.BranchId,
            dailyClose.AccountId,
            dailyClose.Date,
            ct);

        return new ResponseDailyCloseHistoryJson
        {
            DailyCloseId = dailyClose.Id,
            IsHistoryComplete = dailyClose.ReviewHistoryComplete,
            CurrentOpeningSource = currentOpeningSource is null
                ? null
                : new ResponseDailyCloseOpeningSourceJson
                {
                    DailyCloseId = currentOpeningSource.Id,
                    Date = currentOpeningSource.Date
                },
            Events = events
                .Select(reviewEvent => new ResponseDailyCloseHistoryEventJson
                {
                    Id = reviewEvent.Id,
                    Type = reviewEvent.Type,
                    OccurredAt = reviewEvent.OccurredAt,
                    ActorUserId = reviewEvent.ActorUserId,
                    ActorUserName = reviewEvent.ActorUserName,
                    ActorOperatorId = reviewEvent.ActorOperatorId,
                    ActorOperatorName = reviewEvent.ActorOperatorName,
                    RejectionReason = reviewEvent.RejectionReason,
                    SourceDailyCloseId = reviewEvent.SourceDailyCloseId,
                    SourceDate = reviewEvent.SourceDailyClose?.Date,
                    PreviousSourceDailyCloseId = reviewEvent.PreviousSourceDailyCloseId,
                    PreviousSourceDate = reviewEvent.PreviousSourceDailyClose?.Date,
                    AffectedStatusBefore = reviewEvent.AffectedStatusBefore,
                    AffectedStatusAfter = reviewEvent.AffectedStatusAfter,
                    Items = reviewEvent.Items
                        .Where(item => item.Active)
                        .OrderBy(item => item.ProductName, StringComparer.Ordinal)
                        .ThenBy(item => item.ProductId)
                        .Select(item => new ResponseDailyCloseOpeningChangeItemJson
                        {
                            ProductId = item.ProductId,
                            ProductName = item.ProductName,
                            BeforeValue = item.BeforeValue,
                            AfterValue = item.AfterValue,
                            Change = (item.AfterValue ?? 0m) - (item.BeforeValue ?? 0m)
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
