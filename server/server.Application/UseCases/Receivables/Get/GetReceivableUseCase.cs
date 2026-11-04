using server.Application.Services.Receivables;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Receivables.Get;

/// <summary>
/// <c>GET /receivable/{transactionId}</c> (M9.7 Phase 1): any branch row with its settlement event chain, projected at
/// a cutoff — branch-local today on the posted basis by default, or a given date on either basis — with both dates of
/// every event and whether it counts at that cutoff. Manager/Admin; reads only.
/// </summary>
public class GetReceivableUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    ReceivableResponseBuilder responseBuilder,
    IBranchClock branchClock)
{
    public async Task<ResponseReceivableJson> Execute(Guid transactionId, RequestReceivableJson request, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var validation = new GetReceivableFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var row = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(transactionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        var cutoff = new ReceivableCutoff(
            request.CutoffBasis ?? ReceivableCutoffBasis.Posted,
            (request.CutoffDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow())).Date);
        return (await responseBuilder.Build([row], branchUser.BranchId, cutoff, ct))[0];
    }
}
