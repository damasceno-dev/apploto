using server.Domain.Entities;
using server.Utils;

namespace server.Communication.Responses;

public class ResponseFluxoContaJson
{
    public Guid Id { get; set; }
    public ContaTipoEnum Tipo { get; set; }

    public string TipoDescrição => Tipo.GetDescription();
    public string Identificação { get; set; }
    public string Instituição { get; set; }
}