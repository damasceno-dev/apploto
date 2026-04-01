using server.Domain.Entities;

namespace server.Communication.Requests;

public class RequestUserRegisterJson
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Member;
}