using System.ComponentModel;
using server.Utils;

namespace server.Domain.Entities;

public class FluxoClassificação
{
    public Guid Id { get; set; }
    public ClassificaçãoTipoEnum Tipo { get; set; }
    
    public string TipoDescrição => Tipo.GetDescription();
    public string PlanoDeContas { get; set; }
}

public enum ClassificaçãoTipoEnum {
    [Description("Receitas")]
    Receitas=0,
    [Description("Crédito Banco")]
    CréditoBanco=1,
    [Description("Despesas Administrativas")]
    DespesasAdministrativas =2,
    [Description("Despesas Comerciais")]
    DespesasComerciais=3,
}