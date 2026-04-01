using server.Domain.Entities;

namespace server.Communication.Responses;

public class ResponseFluxoJson
{
    public Guid Id { get; set; }
    public DateTime DataLançamento { get; set; }
    public decimal Valor { get; set; }
    public FluxoConta Conta { get; set; }
    
    public FluxoClassificação Classificação { get; set; }
    public IList<FluxoDetalhamento> FluxoDetalhamentos { get; set; }
}