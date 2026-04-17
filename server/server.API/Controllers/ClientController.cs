using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Clients.Create;
using server.Application.UseCases.Clients.Deactivate;
using server.Application.UseCases.Clients.Get;
using server.Application.UseCases.Clients.List;
using server.Application.UseCases.Clients.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class ClientController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseCreateClientJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromServices] CreateClientUseCase createClientUseCase,
        [FromBody] RequestCreateClientJson request)
    {
        var response = await createClientUseCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListClientsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromServices] ListClientsUseCase listClientsUseCase)
    {
        var response = await listClientsUseCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("{clientId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseClientJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetClientUseCase getClientUseCase,
        [FromRoute] Guid clientId)
    {
        var response = await getClientUseCase.Execute(clientId);
        return Ok(response);
    }

    [HttpPut]
    [Route("{clientId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseClientJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateClientUseCase updateClientUseCase,
        [FromRoute] Guid clientId,
        [FromBody] RequestUpdateClientJson request)
    {
        var response = await updateClientUseCase.Execute(clientId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{clientId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseClientJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateClientUseCase deactivateClientUseCase,
        [FromRoute] Guid clientId)
    {
        var response = await deactivateClientUseCase.Execute(clientId);
        return Ok(response);
    }
}
