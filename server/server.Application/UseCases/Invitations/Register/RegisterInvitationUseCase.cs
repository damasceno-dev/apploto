using server.Application.Services;
using server.Application.Services.Security;
using server.Application.UseCases.Users.Login;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Invitations.Register;

public sealed class RegisterInvitationUseCase(
    IUsersRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenServices tokens,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    PasswordEncryption passwords,
    InvitationWorkflow workflow)
{
    public async Task<ResponseUserLoginJson> Execute(RequestRegisterInvitationJson request)
    {
        SecurityValidation.Validate(new RegisterInvitationFluentValidation(), request);
        var candidate = await workflow.FindCapability(request.Token);
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(candidate.BranchId), SecurityRules.EmailKey(candidate.Email));
        var invitation = await workflow.FindCapability(request.Token);
        workflow.RequirePending(invitation);
        if (await users.VerifyIfEmailAlreadyExists(invitation.Email))
            throw new ConflictException(ResourcesErrorMessages.EMAIL_ALREADY_REGISTERED);
        var user = new User { Name = request.Name.Trim(), Email = invitation.Email, Password = passwords.HashPassword(request.Password), CreatedAt = workflow.Now() };
        await users.Register(user);
        await workflow.Join(invitation, user);
        var refresh = refreshTokens.Generate();
        await refreshTokens.SaveRefreshToken(new RefreshToken { UserId = user.Id, Value = refresh, CreatedAt = workflow.Now() });
        var identityToken = tokens.GenerateGlobalToken(user);
        await unitOfWork.Commit();
        await scope.Complete();
        return user.ToResponse(identityToken, refresh);
    }
}
