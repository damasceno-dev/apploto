namespace server.Communication.Requests;

/// <summary>Omit the body to preserve preselection; a body replaces it (null clears it).</summary>
public sealed class RequestReissueInvitationJson
{
    public Guid? OperatorId { get; init; }
}
