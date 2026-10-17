using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Categories.Deactivate;

/// <summary>
/// Soft-deactivates a category and cascades deactivation to all of its active
/// TransactionType children (one-way; re-creating the category later does not
/// auto-reactivate children — the same convention as M2 Account → OperatorAccount).
/// </summary>
public class DeactivateCategoryUseCase(
    IAuthenticationService authenticationService,
    ICategoriesRepository categoriesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task Execute(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.CATEGORY_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var category = await categoriesRepository.GetActiveByIdAndBranchIdWithActiveTransactionTypes(
            categoryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CATEGORY_NOT_FOUND);

        // M7.7 Phase 5.6: deactivating would cascade to a system-owned counterpart type and break physical fiado pairs.
        if (category.TransactionTypes.Any(transactionType => transactionType.SystemRole is not null))
            throw new ConflictException(ResourcesErrorMessages.CATEGORY_SYSTEM_READ_ONLY);

        category.Active = false;
        foreach (var transactionType in category.TransactionTypes)
        {
            transactionType.Active = false;
        }

        await unitOfWork.Commit();
    }
}
