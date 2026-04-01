using server.Domain.Entities;

namespace server.Communication.Requests;

public class RequestFluxoClassificaçãoJson
{
    public ClassificaçãoTipoEnum Tipo { get; set; }
    public string PlanoDeContas { get; set; }
}