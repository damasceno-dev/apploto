using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Infrastructure.Services;

public class JwtTokenService(string signinKey, uint expirationTimeInMinutes) : ITokenServices
{
    private const string TokenScopeClaimType = "loto:token_scope";
    private const string BranchIdClaimType = "loto:branch_id";
    private const string BranchUserIdClaimType = "loto:branch_user_id";

    private SymmetricSecurityKey PrivateKey => new(Encoding.UTF8.GetBytes(signinKey));

    private TokenValidationParameters TokenValidationParameters => new()
    {
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = PrivateKey,
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
    };

    public string GenerateGlobalToken(User user)
    {
        return GenerateToken(
        [
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Sid, user.Id.ToString()),
            new Claim(TokenScopeClaimType, nameof(TokenScope.Global))
        ]);
    }

    public string GenerateBranchToken(BranchUser branchUser)
    {
        return GenerateToken(
        [
            new Claim(ClaimTypes.Sid, branchUser.UserId.ToString()),
            new Claim(ClaimTypes.Role, branchUser.Role.ToString()),
            new Claim(TokenScopeClaimType, nameof(TokenScope.Branch)),
            new Claim(BranchIdClaimType, branchUser.BranchId.ToString()),
            new Claim(BranchUserIdClaimType, branchUser.Id.ToString())
        ]);
    }

    public TokenResultValidation ValidateToken(string token)
    {
        try
        {
            var claimsPrincipal = new JwtSecurityTokenHandler().ValidateToken(token, TokenValidationParameters, out _);
            var userId = Guid.Parse(claimsPrincipal.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
            var scope = ResolveScope(claimsPrincipal);

            if (scope == TokenScope.Global)
            {
                return new TokenResultValidation
                {
                    IsSuccess = true,
                    UserId = userId,
                    Scope = TokenScope.Global,
                    Error = TokenErrorType.None
                };
            }

            var branchId = Guid.Parse(claimsPrincipal.Claims.First(c => c.Type == BranchIdClaimType).Value);
            var branchUserId = Guid.Parse(claimsPrincipal.Claims.First(c => c.Type == BranchUserIdClaimType).Value);
            var role = Enum.Parse<Role>(claimsPrincipal.Claims.First(c => c.Type == ClaimTypes.Role).Value);

            return new TokenResultValidation
            {
                IsSuccess = true,
                UserId = userId,
                BranchId = branchId,
                BranchUserId = branchUserId,
                Role = role,
                Scope = TokenScope.Branch,
                Error = TokenErrorType.None
            };
        }
        catch (SecurityTokenExpiredException)
        {
            return new TokenResultValidation
            {
                IsSuccess = false,
                Error = TokenErrorType.Expired
            };
        }
        catch (Exception)
        {
            return new TokenResultValidation
            {
                IsSuccess = false,
                Error = TokenErrorType.Invalid
            };
        }
    }

    private string GenerateToken(IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityTokenHandler().CreateJwtSecurityToken(
            subject: new ClaimsIdentity(claims),
            expires: DateTime.UtcNow.AddMinutes(expirationTimeInMinutes),
            signingCredentials: new SigningCredentials(PrivateKey, SecurityAlgorithms.HmacSha256Signature));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static TokenScope ResolveScope(ClaimsPrincipal claimsPrincipal)
    {
        var scopeValue = claimsPrincipal.Claims.FirstOrDefault(claim => claim.Type == TokenScopeClaimType)?.Value;

        return Enum.TryParse<TokenScope>(scopeValue, true, out var scope)
            ? scope
            : TokenScope.Global;
    }
}
