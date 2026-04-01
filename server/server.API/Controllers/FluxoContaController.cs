using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.FluxoContas.GetAll;
using server.Application.UseCases.FluxoContas.GetById;
using server.Application.UseCases.FluxoContas.Register;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Filters;

namespace server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FluxoContaController : ControllerBase
    {
        [HttpPost]
        [Route("register")]
        [ProducesResponseType(typeof(ResponseFluxoContaJson), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RequestFluxoContaJson request, [FromServices]FluxoContaRegisterUseCase fluxoContaRegisterUseCase)
        {
            var response = await fluxoContaRegisterUseCase.Execute(request);
            return Created(string.Empty, response);
        }
        
        [HttpGet]
        [Route("{fluxoContaId}/getById")]
        [MyTokenAuthorizeFilter(Role.Admin, Role.Manager)]
        [ProducesResponseType(typeof(ResponseFluxoContaJson), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById([FromRoute] Guid fluxoContaId, [FromServices]FluxoContaGetByIdUseCase fluxoContaGetByIdUseCase)
        {
            var response = await fluxoContaGetByIdUseCase.Execute(fluxoContaId);
            return Ok(response);
        }
        
        [HttpGet]
        [Route("getAll")]
        [MyTokenAuthenticateFilter]
        [ProducesResponseType(typeof(ResponseFluxoContaJson), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll([FromServices]FluxoContaGetAllUseCase fluxoContaGetAllUseCase)
        {
            var response = await fluxoContaGetAllUseCase.Execute();
            return Ok(response);
        }
    }
}