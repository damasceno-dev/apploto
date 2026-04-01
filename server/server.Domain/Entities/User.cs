namespace server.Domain.Entities;

public class User : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Member;
}

public enum Role
{
    Admin = 0,
    Manager = 1,
    Member = 2
}