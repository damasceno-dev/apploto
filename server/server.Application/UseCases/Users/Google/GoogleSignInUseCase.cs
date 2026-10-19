using server.Application.Services;
using server.Application.Services.Security;
using server.Application.UseCases.Users.Login;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.Google;

public sealed class GoogleSignInUseCase(IGoogleIdentityVerifier verifier, IGoogleIdentityRepository identities,
    IUsersRepository users, IRefreshTokenRepository refreshTokens, ITokenServices tokens,
    ISecurityCoordination coordination, IUnitOfWork unitOfWork, InvitationWorkflow invitations,
    IAccountSecurityRepository security, PasswordEncryption passwords, TimeProvider time)
{
    public async Task<ResponseUserLoginJson> Execute(RequestGoogleSignInJson request, CancellationToken ct = default)
    {
        if (!new GoogleSignInFluentValidation().Validate(request).IsValid) throw Invalid();
        if (!verifier.IsConfigured)
            throw new GoogleProviderUnavailableException(ResourcesErrorMessages.GOOGLE_NOT_CONFIGURED);
        var result = await verifier.Verify(request.IdToken, ct);
        if (result.ProviderUnavailable)
            throw new GoogleProviderUnavailableException(ResourcesErrorMessages.GOOGLE_PROVIDER_UNAVAILABLE);
        var google = result.Identity;
        if (google is null || !google.EmailVerified || string.IsNullOrWhiteSpace(google.Subject) ||
            string.IsNullOrWhiteSpace(google.Nonce)) throw Invalid();
        var email = await users.NormalizeEmail(google.Email);
        // A linked subject may keep signing in after its provider email changes. Coordinate
        // against the local account email too, so reset/renew/logout cannot race this session.
        var previousLink = await identities.GetBySubject(google.Issuer, google.Subject);
        var linkedUser = previousLink is null ? null : await users.GetById(previousLink.UserId);
        Invitation? candidate = null;
        if (!string.IsNullOrEmpty(request.InvitationToken)) candidate = await invitations.FindCapability(request.InvitationToken);
        var keys = new List<string> { "google-challenge:" + request.ChallengeId.ToString("N"),
            "google-subject:" + SecurityRules.Hash(google.Issuer + ":" + google.Subject), SecurityRules.EmailKey(email) };
        if (linkedUser is not null) keys.Add(SecurityRules.EmailKey(linkedUser.Email));
        if (candidate is not null) keys.Add(SecurityRules.BranchKey(candidate.BranchId));
        await using var scope = await SecurityRules.Acquire(coordination, keys.ToArray());
        var challenge = await identities.GetChallenge(request.ChallengeId);
        var now = time.GetUtcNow().UtcDateTime;
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.ExpiresAt <= now ||
            challenge.StateHash != SecurityRules.Hash(request.State) ||
            challenge.NonceHash != SecurityRules.Hash(google.Nonce)) throw ChallengeInvalid();

        Invitation? invitation = null;
        if (candidate is not null)
        {
            invitation = await invitations.FindCapability(request.InvitationToken!);
            invitations.RequirePending(invitation);
            if (invitation.Email != email)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.INVITATION_EMAIL_MISMATCH);
        }

        var link = await identities.GetBySubject(google.Issuer, google.Subject);
        User user;
        if (link is not null)
        {
            user = await security.GetUserForUpdate(link.UserId) ?? throw Invalid();
            if (invitation is not null && user.Email != invitation.Email)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.INVITATION_EMAIL_MISMATCH);
        }
        else
        {
            var existing = await users.GetByEmail(email);
            if (existing is not null)
            {
                user = await security.GetUserForUpdate(existing.Id) ?? throw Invalid();
                if (await identities.GetByUserId(user.Id) is not null)
                    throw new ConflictException(ResourcesErrorMessages.GOOGLE_IDENTITY_CONFLICT);
                // A verified Google email alone cannot take over a legacy password account.
                // Auto-link only where both sides have explicit, recorded email proof and
                // Google is authoritative for that mailbox; otherwise require its password.
                if (!(google.GoogleAuthoritative && user.EmailVerifiedAt.HasValue) &&
                    (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(user.Password) ||
                     !passwords.VerifyPassword(request.CurrentPassword, user.Password)))
                    throw new ConflictException(ResourcesErrorMessages.GOOGLE_LINK_PROOF_REQUIRED);
            }
            else
            {
                if (invitation is null) throw Invalid();
                if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 255) throw Invalid();
                user = new User { Name = request.Name.Trim(), Email = email, Password = string.Empty,
                    EmailVerifiedAt = now, CreatedAt = now };
                await users.Register(user);
            }
            await identities.AddIdentity(new GoogleIdentity
            {
                UserId = user.Id, User = user, Issuer = google.Issuer, Subject = google.Subject
            });
        }
        if (invitation is not null)
        {
            await invitations.Join(invitation, user);
            user.EmailVerifiedAt ??= now;
        }
        challenge.ConsumedAt = now;
        var refresh = refreshTokens.Generate();
        await refreshTokens.SaveRefreshToken(new RefreshToken { UserId = user.Id, Value = refresh, CreatedAt = now });
        var identityToken = tokens.GenerateGlobalToken(user);
        await unitOfWork.Commit(ct);
        await scope.Complete();
        return user.ToResponse(identityToken, refresh);
    }

    private static InvalidLoginException Invalid() => new(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID);
    private static InvalidLoginException ChallengeInvalid() => new(ResourcesErrorMessages.GOOGLE_CHALLENGE_INVALID);
}
