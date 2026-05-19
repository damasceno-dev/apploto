namespace server.Communication.Responses;

public class ResponseListTransactionTypesJson
{
    public IReadOnlyList<ResponseTransactionTypeJson> Items { get; set; } = [];
}
