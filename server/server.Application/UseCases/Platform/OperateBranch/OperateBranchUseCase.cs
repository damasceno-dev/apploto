using System.Text.Json;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Platform.OperateBranch;

public sealed record OperatedBranchResult(ResponseOperatedOnboardingJson Response, bool Created);

public sealed class OperateBranchUseCase(
    IAuthenticationService authentication, IAccountSecurityRepository security,
    IUsersRepository users, IOperatedOnboardingRepository onboardings,
    IOnboardingRepository invitations, IAccountsRepository accounts,
    IOperatorsRepository operators, IOperatorAccountsRepository grants, IProductsRepository products,
    BranchBootstrap bootstrap, InvitationWorkflow workflow,
    ISecurityCoordination coordination, IUnitOfWork unitOfWork,
    ITerminalsRepository terminalsRepository)
{
    public async Task<OperatedBranchResult> Execute(RequestOperateBranchJson request)
    {
        SecurityValidation.Validate(new OperateBranchFluentValidation(), request);
        var actor = await authentication.GetAuthenticatedUser();
        var email = await users.NormalizeEmail(request.OwnerEmail);
        var hash = SecurityRules.Hash(JsonSerializer.Serialize(new
        {
            request.RequestId, Name = request.Name.Trim(), Cnpj = request.Cnpj?.Trim(),
            Address = request.Address?.Trim(), Phone = request.Phone?.Trim(), Email = email,
            Terminals = request.Terminals.Select(x => new { Name = x.Name.Trim(), PairedTabName = x.PairedTabName?.Trim() }),
            Products = request.AdditionalProducts.Select(x => x.Trim()),
            Operators = request.Operators.Select(x => new { Name = x.Name.Trim(), x.TerminalIndexes })
        }));
        var token = SecurityRules.NewToken();
        OperatedOnboarding record;
        await using (var scope = await SecurityRules.Acquire(coordination, "onboarding:" + request.RequestId.ToString("N"),
            SecurityRules.EmailKey(actor.Email), SecurityRules.EmailKey(email)))
        {
            var fresh = await SecurityRules.RequireUser(security, actor.Id);
            if (!fresh.IsPlatformOperator) throw new TokenWithoutPermissionException(ResourcesErrorMessages.PLATFORM_ONLY);
            var existing = await onboardings.Get(request.RequestId);
            if (existing is not null)
            {
                if (existing.PlatformUserId != fresh.Id || existing.RequestHash != hash)
                    throw new ConflictException(ResourcesErrorMessages.ONBOARDING_REQUEST_CONFLICT);
                await scope.Complete();
                return new OperatedBranchResult(Map(existing, workflow.Now()), false);
            }
            var owner = await users.GetByEmail(email);
            if (owner is { Active: false }) throw new ConflictException(ResourcesErrorMessages.INVITATION_TARGET_UNAVAILABLE);
            var branch = await bootstrap.Create(new RequestCreateBranchJson
            {
                Name = request.Name.Trim(), Cnpj = request.Cnpj?.Trim(), Address = request.Address?.Trim(), Phone = request.Phone?.Trim()
            });
            var terminals = new List<Account>();
            foreach (var setup in request.Terminals)
            {
                Account? tab = null;
                if (setup.PairedTabName is not null)
                {
                    tab = new Account { BranchId = branch.Id, Name = setup.PairedTabName.Trim(), Type = AccountType.Tab };
                    await accounts.Add(tab);
                }
                // Each seeded cashbox gets its own physical CAIXA terminal (M9.6 Phase 2).
                var physicalTerminal = new Terminal { BranchId = branch.Id, Name = setup.Name.Trim() };
                await terminalsRepository.Add(physicalTerminal);
                var terminal = new Account { BranchId = branch.Id, Name = setup.Name.Trim(), Type = AccountType.Terminal,
                    TabAccountId = tab?.Id, TerminalId = physicalTerminal.Id };
                await accounts.Add(terminal);
                terminals.Add(terminal);
            }
            var highestSeededOrder = server.Application.UseCases.Branches.Create.CreateBranchSeedFactory.CreateDefaultProducts(branch.Id)
                .Max(product => product.DisplayOrder);
            for (var i = 0; i < request.AdditionalProducts.Count; i++)
                await products.Add(new Product { BranchId = branch.Id, Name = request.AdditionalProducts[i].Trim(), DisplayOrder = highestSeededOrder + i + 1 });
            foreach (var setup in request.Operators)
            {
                var op = new Operator { BranchId = branch.Id, Name = setup.Name.Trim() };
                await operators.Add(op);
                for (var i = 0; i < setup.TerminalIndexes.Count; i++)
                {
                    var terminal = terminals[setup.TerminalIndexes[i]];
                    await grants.Add(new OperatorAccount { OperatorId = op.Id, AccountId = terminal.Id, IsPrimary = i == 0 });
                    if (terminal.TabAccountId.HasValue)
                        await grants.Add(new OperatorAccount { OperatorId = op.Id, AccountId = terminal.TabAccountId.Value });
                }
            }
            var now = workflow.Now();
            var invite = new Invitation { BranchId = branch.Id, Email = email, Role = Role.Admin,
                IssuedByUserId = fresh.Id, TokenHash = SecurityRules.Hash(token), ExpiresAt = now.Add(workflow.InvitationLifetime), CreatedAt = now };
            await invitations.AddInvitation(invite);
            record = new OperatedOnboarding { RequestId = request.RequestId, BranchId = branch.Id,
                PlatformUserId = fresh.Id, OwnerInvitationId = invite.Id, RequestHash = hash,
                Branch = branch, OwnerInvitation = invite, CreatedAt = now };
            await onboardings.Add(record);
            await unitOfWork.Commit();
            await scope.Complete();
        }
        await workflow.Deliver(record.OwnerInvitation, token);
        return new OperatedBranchResult(Map(record, workflow.Now()), true);
    }

    public static ResponseOperatedOnboardingJson Map(OperatedOnboarding record, DateTime now) => new()
    {
        RequestId = record.RequestId, BranchId = record.BranchId, BranchName = record.Branch.Name,
        OwnerInvitationId = record.OwnerInvitationId, OwnerEmail = record.OwnerInvitation.Email,
        OwnerInvitationStatus = record.OwnerInvitation.Status == InvitationStatus.Pending && record.OwnerInvitation.ExpiresAt <= now
            ? InvitationStatus.Expired : record.OwnerInvitation.Status,
        DeliveryStatus = record.OwnerInvitation.DeliveryStatus, ExpiresAt = record.OwnerInvitation.ExpiresAt
    };
}
