using server.Domain.Entities;

namespace server.Communication.Requests;

public class RequestFluxoContaJson
{
    public ContaTipoEnum Tipo { get; set; }
    public string Identificação { get; set; }
    public string Instituição { get; set; }
}