using server.Application.Services.Security;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Openings;

public class ListGoLiveOpeningsUseCase(IAuthenticationService authentication, IBranchUsersRepository memberships, IGoLiveOpeningsRepository openings)
{
    public async Task<IReadOnlyList<ResponseGoLiveOpeningJson>> Execute(CancellationToken ct = default)
    {
        var actor = await SecurityRules.FreshActor(memberships, await authentication.GetAuthenticatedBranchUser());
        if (actor.Role is not Role.Manager and not Role.Admin) throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        var result = new List<ResponseGoLiveOpeningJson>();
        foreach (var row in await openings.List(actor.BranchId, ct))
        {
            var items = await openings.Rows(actor.BranchId, row.Id, ct);
            result.Add(new ResponseGoLiveOpeningJson
            {
                Id = row.Id,
                Version = row.Version,
                AccountId = row.AccountId,
                ClientId = row.ClientId,
                IsCheque = row.IsCheque,
                AsOfDate = row.AsOfDate,
                SourceReference = row.SourceReference,
                SourceEvidence = row.SourceEvidence,
                PreviousId = row.PreviousId,
                SourceImportId = await openings.GetSourceImportId(actor.BranchId, row.Id, ct),
                Active = row.Active,
                TransactionIds = items.Select(x => x.Id).ToList(),
                RecordedByOperatorId = items.FirstOrDefault()?.RecordedByOperatorId ?? Guid.Empty,
                ClientCreditObserved = row.ClientCreditObservedAt is not null,
                Items = items.Select(x => new ResponseGoLiveOpeningItemJson { Id = x.Id, Value = x.Value, DueDate = x.DueDate, Status = x.Status }).ToList()
            });
        }
        return result;
    }
}
