namespace server.Communication.Responses;

public class ResponseListProductsJson
{
    public IReadOnlyList<ResponseProductJson> Items { get; set; } = [];
}
