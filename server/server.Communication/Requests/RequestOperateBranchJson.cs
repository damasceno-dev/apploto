namespace server.Communication.Requests;

public sealed class RequestOperateBranchJson
{
    public Guid RequestId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Cnpj { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string OwnerEmail { get; init; } = string.Empty;
    public List<RequestOnboardingTerminalJson> Terminals { get; init; } = [];
    public List<string> AdditionalProducts { get; init; } = [];
    public List<RequestOnboardingOperatorJson> Operators { get; init; } = [];
}

public sealed class RequestOnboardingTerminalJson
{
    public string Name { get; init; } = string.Empty;
    public string? PairedTabName { get; init; }
}

public sealed class RequestOnboardingOperatorJson
{
    public string Name { get; init; } = string.Empty;
    public List<int> TerminalIndexes { get; init; } = [];
}
