// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using server.Entities;
// using server.Requests;
// using server.Responses;
//
// namespace server.Controllers
// {
//     // [Route("api/[controller]")]
//     // [ApiController]
//     public class FluxoController : ControllerBase
//     {
//         private readonly LotoDbContext _dbContext;
//
//         public FluxoController(LotoDbContext dbContext)
//         {
//             _dbContext = dbContext;
//         }
//
//         [HttpPost]
//         public async Task<IActionResult> Register([FromBody] RequestFluxoJson request)
//         {
//             var novoFluxo = new Fluxo
//             {
//                 DataLançamento = request.DataLançamento,
//                 Valor = request.Valor,
//                 ContaId = request.ContaId,
//                 ClassificaçãoId = request.ClassificaçãoId
//             };
//
//             _dbContext.Fluxos.Add(novoFluxo);
//             await _dbContext.SaveChangesAsync();
//
//             return Created(string.Empty, novoFluxo);
//         }
//
//         [HttpGet]
//         public async Task<IActionResult> getAll()
//         {
//             var response = new List<ResponseFluxoJson>();
//             var fluxos = await _dbContext.Fluxos.Include(f => f.FluxoDetalhamentos).ToListAsync();
//             foreach (var fluxo in fluxos)
//             {
//                 fluxo.Conta = await _dbContext.FluxoContas.FindAsync(fluxo.ContaId);
//                 fluxo.Classificação = await _dbContext.FluxoClassificações.FindAsync(fluxo.ClassificaçãoId);
//                 
//                 response.Add((new ResponseFluxoJson
//                 {
//                     Id = fluxo.Id,
//                     DataLançamento = fluxo.DataLançamento,
//                     Valor = fluxo.Valor,
//                     Conta = fluxo.Conta,
//                     Classificação = fluxo.Classificação,
//                     FluxoDetalhamentos = fluxo.FluxoDetalhamentos
//                 }));
//             }
//             return Ok(response);
//         }
//
//     }
// }
