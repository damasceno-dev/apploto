namespace server.Communication.Responses;

public class ResponseCreateTransactionInstallmentJson
{
    public IReadOnlyList<ResponseCreateTransactionJson> Installments { get; set; } = [];
}
