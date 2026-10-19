using server.Application.Services.Security;
using server.Application.UseCases.Platform.OperateBranch;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Platform.ReadOnboarding;

public sealed class ReadOperatedOnboardingUseCase(IAuthenticationService authentication, IAccountSecurityRepository security,
    IOperatedOnboardingRepository records, ISecurityCoordination coordination, TimeProvider time)
{
    public async Task<ResponseOperatedOnboardingJson> Execute(Guid requestId)
    {
        var actor = await authentication.GetAuthenticatedUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(actor.Email));
        var fresh = await SecurityRules.RequireUser(security, actor.Id);
        if (!fresh.IsPlatformOperator) throw new TokenWithoutPermissionException(ResourcesErrorMessages.PLATFORM_ONLY);
        var record = await records.Get(requestId) ?? throw new NotFoundException(ResourcesErrorMessages.ONBOARDING_NOT_FOUND);
        await scope.Complete();
        return OperateBranchUseCase.Map(record, time.GetUtcNow().UtcDateTime);
    }
}
