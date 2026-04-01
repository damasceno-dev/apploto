namespace server.Domain.Entities;

public class Fluxo
{
    public Guid Id { get; set; }
    public DateTime DataLançamento { get; set; }
    public decimal Valor { get; set; }
    public Guid ContaId { get; set; }
    public FluxoConta Conta { get; set; }
    
    public Guid ClassificaçãoId { get; set; }
    public FluxoClassificação Classificação { get; set; }
    public IList<FluxoDetalhamento> FluxoDetalhamentos { get; set; }
}