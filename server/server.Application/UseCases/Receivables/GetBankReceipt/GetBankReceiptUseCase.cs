using server.Application.Services.Receivables;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Receivables.GetBankReceipt;

/// <summary><c>GET /bank-receipt/{id}</c> (M9.7 Phase 2.2): a pending receipt with its allocations. Manager/Admin.</summary>
public class GetBankReceiptUseCase(IAuthenticationService authenticationService, ReceivableResponseBuilder responseBuilder)
{
    public async Task<ResponseBankReceiptJson> Execute(Guid receiptId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        return await responseBuilder.BuildReceipt(receiptId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.BANK_RECEIPT_NOT_FOUND);
    }
}
