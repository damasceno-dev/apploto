namespace server.Domain.Entities;

public class User : EntityBase
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public ICollection<BranchUser> BranchUsers { get; init; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; init; } = [];
}
