using server.Domain.Entities;

namespace server.Communication.Requests
{
    public class RequestFluxoDetalhamentoJson
    {
        public DetalhamentoTipoEnum Tipo { get; set; }
    
        public string Valor { get; set; }
    }
}