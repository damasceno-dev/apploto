using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestApproveSessionJson
{
    public SessionApprovalMode Mode { get; init; }

    /// <summary>Required for <c>SingleActiveManager</c>.</summary>
    public string? Reason { get; init; }
}

/// <summary>Aprovar dia do caixa: an explicit list of sessions of one cashbox day, each with the version read.</summary>
public class RequestApproveSessionBatchJson
{
    public IReadOnlyList<RequestApproveSessionBatchItemJson>? Sessions { get; init; }
    public SessionApprovalMode Mode { get; init; }
    public string? Reason { get; init; }
}

public class RequestApproveSessionBatchItemJson
{
    public Guid SessionId { get; init; }
    public uint Version { get; init; }
}
