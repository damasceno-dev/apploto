namespace server.Communication.Requests;

public class RequestFluxoJson
{
    public DateTime DataLançamento { get; set; }
    public decimal Valor { get; set; }
    public Guid ContaId { get; set; }
    public Guid ClassificaçãoId { get; set; }
}