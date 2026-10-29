using FluentValidation;
using server.Application.Services.Sessions;
using server.Application.UseCases.Accounts;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Terminals;

public class CreateTerminalUseCase(
    IAuthenticationService authenticationService,
    ITerminalsRepository terminalsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTerminalJson> Execute(RequestCreateTerminalJson request, CancellationToken ct = default)
    {
        var validation = new CreateTerminalFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var terminal = new Terminal
        {
            BranchId = branchUser.BranchId,
            Name = request.Name.Trim(),
            ExternalReference = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference.Trim()
        };
        await terminalsRepository.Add(terminal, ct);
        await unitOfWork.Commit(ct);
        return terminal.ToResponse();
    }
}

public class ListTerminalsUseCase(
    IAuthenticationService authenticationService,
    ITerminalsRepository terminalsRepository)
{
    public async Task<IReadOnlyList<ResponseTerminalJson>> Execute(CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var terminals = await terminalsRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId, ct);
        return terminals.Select(terminal => terminal.ToResponse()).ToList();
    }
}

/// <summary>
/// Reassigns a cashbox to another physical terminal: manager-only, refused while the cashbox has active custody or a
/// pending handover. Earlier sessions keep their TerminalId snapshot. Takes the full §6.20 global order.
/// </summary>
public class AssignCashboxTerminalUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    ITerminalsRepository terminalsRepository,
    IDailyClosesRepository dailyClosesRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    IDailyCloseAccountCoordination accountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAccountJson> Execute(
        Guid accountId,
        RequestAssignCashboxTerminalJson request,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        await using var coordination = await accountCoordination.AcquireWithPairing(branchUser.BranchId, [accountId], ct);

        var cashbox = await accountsRepository.GetActiveByIdAndBranchId(accountId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        if (cashbox.Type != AccountType.Terminal)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);
        _ = await terminalsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.TerminalId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TERMINAL_NOT_FOUND);

        if (await dailyClosesRepository.GetActiveCustodyByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct) is not null
            || await sessionHandoversRepository.ExistsUnresolvedByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct))
        {
            throw new ConflictException(ResourcesErrorMessages.ACCOUNT_TERMINAL_REASSIGN_ACTIVE_CUSTODY);
        }

        cashbox.TerminalId = request.TerminalId;
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return cashbox.ToAccountResponse();
    }
}

internal static class TerminalMapper
{
    extension(Terminal terminal)
    {
        public ResponseTerminalJson ToResponse()
        {
            return new ResponseTerminalJson
            {
                Id = terminal.Id,
                Name = terminal.Name,
                ExternalReference = terminal.ExternalReference,
                Active = terminal.Active
            };
        }
    }
}

public class CreateTerminalFluentValidation : FluentValidation.AbstractValidator<RequestCreateTerminalJson>
{
    public CreateTerminalFluentValidation()
    {
        RuleFor(request => request.Name)
            .Must(name => string.IsNullOrWhiteSpace(name) is false)
            .WithMessage(ResourcesErrorMessages.TERMINAL_NAME_REQUIRED)
            .MaximumLength(255)
            .WithMessage(ResourcesErrorMessages.TERMINAL_NAME_TOO_LONG);
        RuleFor(request => request.ExternalReference)
            .MaximumLength(100)
            .WithMessage(ResourcesErrorMessages.TERMINAL_REFERENCE_TOO_LONG);
    }
}
