// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using server.Entities;
// using server.Requests;
//
// namespace server.Controllers
// {
//     // [Route("api/[controller]")]
//     // [ApiController]
//     public class FluxoDetalhamentoController : ControllerBase
//     {
//         private readonly LotoDbContext _dbContext;
//
//         public FluxoDetalhamentoController(LotoDbContext dbContext)
//         {
//             _dbContext = dbContext;
//         }
//
//         // [HttpPost]
//         // [Route("{fluxoId}")]
//         public async Task<IActionResult> Register(RequestFluxoDetalhamentoJson request, [FromRoute] Guid fluxoId)
//         {
//             var novoFluxoDetalhamento = new FluxoDetalhamento
//             {
//                 Tipo = request.Tipo,
//                 Valor = request.Valor,
//                 FluxoId = fluxoId
//             };
//             
//             _dbContext.FluxoDetalhamentos.Add(novoFluxoDetalhamento);
//             await _dbContext.SaveChangesAsync();
//             return Ok(novoFluxoDetalhamento);
//         }
//     }
// }
