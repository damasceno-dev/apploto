using System.ComponentModel;
using server.Utils;

namespace server.Domain.Entities;

public class FluxoConta
{
    public Guid Id { get; set; }
    public ContaTipoEnum Tipo { get; set; }

    public string TipoDescrição => Tipo.GetDescription();
    public string Identificação { get; set; }
    public string Instituição { get; set; }
}

public enum ContaTipoEnum
{
    [Description("Terminal")]
    Terminal=0,
    [Description("Conta Corrente PJ")]
    ContaCorrentePj=1,
    [Description("Conta Corrente")]
    ContaCorrente=2,
}