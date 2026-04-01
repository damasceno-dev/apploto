using System.ComponentModel;
using server.Utils;

namespace server.Domain.Entities;

public class FluxoDetalhamento
{
    public Guid Id { get; set; }
    public DetalhamentoTipoEnum Tipo { get; set; }
    
    public string TipoDescrição => Tipo.GetDescription();
    public string Valor { get; set; }
    public Guid FluxoId { get; set; }
}

public enum DetalhamentoTipoEnum
{
    [Description("Descrição")]
    Descrição,
    [Description("Cliente")]
    Cliente,
    [Description("Vencimento")]
    Vencimento,
    [Description("Data de Pagamento")]
    DataPagamento,
    [Description("Condição")]
    Condição
}