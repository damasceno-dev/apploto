// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using server.Entities;
// using server.Requests;
//
// namespace server.Controllers
// {
//     // [Route("api/[controller]")]
//     // [ApiController]
//     public class FluxoClassificaçãoController : ControllerBase
//     {
//         private readonly LotoDbContext _dbContext;
//
//         public FluxoClassificaçãoController(LotoDbContext dbContext)
//         {
//             _dbContext = dbContext;
//         }
//         
//         [HttpPost]
//         public async Task<IActionResult> Register([FromBody] RequestFluxoClassificaçãoJson request)
//         {
//             var novaClassificação = new FluxoClassificação
//             {
//                 Tipo = request.Tipo,
//                 PlanoDeContas = request.PlanoDeContas,
//             };
//
//             _dbContext.FluxoClassificações.Add(novaClassificação);
//             await _dbContext.SaveChangesAsync();
//             
//             return Ok(novaClassificação);
//         }
//
//         [HttpGet]
//         public async Task<IActionResult> GetAll()
//         {
//             return Ok(await _dbContext.FluxoClassificações.ToListAsync());
//         }
//     }
// } 
