using FluentValidation;
using server.Application.Services.DailyCloses;
using server.Application.Services.Sessions;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.OpeningCount;

/// <summary>
/// RecordOpeningCount (M9.6 Phase 3.0, saldo de implantação): a Manager/Admin records the go-live counted value of
/// each product of a cashbox. It becomes the proposed opening of the cashbox's first session and carries no variance,
/// close status, debt or financial row. Refused once the cashbox has a counted session, active custody or unresolved
/// history; each record appends a new revision.
/// </summary>
public class RecordOpeningCountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IProductsRepository productsRepository,
    IDailyClosesRepository dailyClosesRepository,
    IOpeningCountsRepository openingCountsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    SessionCommandRunner commandRunner,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseOpeningCountJson> Execute(RequestRecordOpeningCountJson request, CancellationToken ct = default)
    {
        var validation = new RecordOpeningCountFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(branchUser);

        return await commandRunner.RunGuarded<ResponseOpeningCountJson>(
            branchUser,
            [request.AccountId],
            async () =>
            {
                var now = branchClock.UtcNow();
                if (request.AsOfDate.Date > branchClock.LocalBusinessDate(now))
                    throw new OnValidationException([ResourcesErrorMessages.OPENING_COUNT_FUTURE_DATE]);

                var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.AccountId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
                if (cashbox.Type != AccountType.Terminal)
                    throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);

                if (await dailyClosesRepository.GetLatestCountedReadyByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct) is not null
                    || await dailyClosesRepository.GetActiveCustodyByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct) is not null
                    || await dailyClosesRepository.HasUnresolvedHistoryByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct))
                {
                    throw new ConflictException(ResourcesErrorMessages.OPENING_COUNT_NOT_ALLOWED);
                }

                var items = request.Items ?? [];
                var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
                var productIds = items.Select(item => item.ProductId).ToList();
                if (productIds.Contains(cashVarianceProductId))
                    throw new OnValidationException([ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN]);
                if (productIds.Count > 0
                    && (await productsRepository.ListActiveByIdsAndBranchIdAsNoTracking(productIds, branchUser.BranchId, ct)).Count != productIds.Count)
                {
                    throw new NotFoundException(ResourcesErrorMessages.SESSION_OPENING_PRODUCT_NOT_FOUND);
                }

                var current = await openingCountsRepository.GetCurrentByAccountId(branchUser.BranchId, cashbox.Id, ct);
                if (current?.Revision != request.ExpectedRevision)
                    throw new ConflictException(ResourcesErrorMessages.OPENING_COUNT_STALE);

                if (current is not null)
                {
                    current.Active = false;
                    await unitOfWork.Commit(ct);
                }

                var openingCount = new Domain.Entities.OpeningCount
                {
                    BranchId = branchUser.BranchId,
                    AccountId = cashbox.Id,
                    AsOfDate = request.AsOfDate.Date,
                    Revision = (current?.Revision ?? 0) + 1,
                    RecordedByUserId = branchUser.UserId,
                    CreatedAt = now
                };
                foreach (var item in items)
                {
                    openingCount.Items.Add(new OpeningCountItem
                    {
                        OpeningCountId = openingCount.Id,
                        ProductId = item.ProductId,
                        Value = item.Value,
                        CreatedAt = now
                    });
                }
                await openingCountsRepository.Add(openingCount, ct);

                return async () =>
                {
                    var saved = await openingCountsRepository.GetCurrentByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct);
                    return saved!.ToResponse();
                };
            },
            ct);
    }
}

public class GetOpeningCountUseCase(
    IAuthenticationService authenticationService,
    IOpeningCountsRepository openingCountsRepository)
{
    public async Task<ResponseOpeningCountJson> Execute(Guid accountId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(branchUser);
        var openingCount = await openingCountsRepository.GetCurrentByAccountIdAsNoTracking(branchUser.BranchId, accountId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_OPENING_SOURCE_MISSING);
        return openingCount.ToResponse();
    }
}

internal static class OpeningCountMapper
{
    extension(Domain.Entities.OpeningCount openingCount)
    {
        public ResponseOpeningCountJson ToResponse()
        {
            return new ResponseOpeningCountJson
            {
                Id = openingCount.Id,
                Version = openingCount.Version,
                AccountId = openingCount.AccountId,
                AsOfDate = openingCount.AsOfDate,
                Revision = openingCount.Revision,
                RecordedByUserId = openingCount.RecordedByUserId,
                CreatedAt = openingCount.CreatedAt,
                Items = openingCount.Items
                    .OrderBy(item => item.ProductId)
                    .Select(item => new ResponseSessionHandoverItemJson { ProductId = item.ProductId, Value = item.Value })
                    .ToList()
            };
        }
    }
}

public class RecordOpeningCountFluentValidation : FluentValidation.AbstractValidator<RequestRecordOpeningCountJson>
{
    public RecordOpeningCountFluentValidation()
    {
        RuleFor(request => request.AccountId).NotEmpty().WithMessage(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        RuleFor(request => request.Items).OpeningItems();
    }
}
