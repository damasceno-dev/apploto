namespace server.Communication.Responses;

public class ResponseFiadoBalanceImpactJson
{
    public IReadOnlyList<ResponseClientBalanceDeltaJson> Deltas { get; init; } = [];
}
