namespace server.Communication.Requests;

public class RequestCreateBranchJson
{
    public string Name { get; init; } = string.Empty;
    public string? Cnpj { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
}
