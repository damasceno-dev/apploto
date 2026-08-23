namespace server.Communication.Responses;

public class ResponseCreateTransactionInstallmentJson
{
    public uint Version { get; set; }
    public IReadOnlyList<ResponseCreateTransactionJson> Installments { get; set; } = [];
}
