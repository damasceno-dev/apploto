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
    private SymmetricSecurityKey PrivateKey => new(Encoding.UTF8.GetBytes(signinKey));
    private TokenValidationParameters TokenValidationParameters => new()
    {
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = PrivateKey,
        ValidateAudience = false,
        ValidateIssuer = false,
    };
    public string Generate(User user)
    {
        var token = new JwtSecurityTokenHandler().CreateJwtSecurityToken(
                subject: new ClaimsIdentity([
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Sid, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                ]),
                expires:DateTime.UtcNow.AddMinutes(expirationTimeInMinutes),
                signingCredentials: new SigningCredentials(PrivateKey, SecurityAlgorithms.HmacSha256Signature)
            );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenResultValidation ValidateToken(string token)
    {
        try
        {
            var claimsPrincipal = new JwtSecurityTokenHandler().ValidateToken(token, TokenValidationParameters, out _);
            var userId = Guid.Parse(claimsPrincipal.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
            return new TokenResultValidation { IsSuccess = true, UserId = userId, Error = TokenErrorType.None};
        }
        catch (SecurityTokenExpiredException)
        {
            return new TokenResultValidation { IsSuccess = false, Error = TokenErrorType.Expired };
        }
        catch (Exception)
        {
            return new TokenResultValidation { IsSuccess = false, Error = TokenErrorType.Invalid };
        }
        
    }
}