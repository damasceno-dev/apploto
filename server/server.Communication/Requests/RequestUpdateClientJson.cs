namespace server.Communication.Requests;

public class RequestUpdateClientJson
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? Cpf { get; init; }
    public string? Cep { get; init; }
    public string? Address { get; init; }
    public string? PhoneSecondary { get; init; }
    public string? Notes { get; init; }
    public string? Email { get; init; }
}
