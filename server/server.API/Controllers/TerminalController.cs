using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Sessions.Terminals;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

/// <summary>Physical CAIXA terminals (M9.6 Phase 2): the report reference of cashboxes, never a custody key.</summary>
[Route("terminal")]
[ApiController]
public class TerminalController : ControllerBase
{
    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(IReadOnlyList<ResponseTerminalJson>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromServices] ListTerminalsUseCase useCase,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(cancellationToken));
    }

    [HttpPost]
    [Route("")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseTerminalJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromServices] CreateTerminalUseCase useCase,
        [FromBody] RequestCreateTerminalJson request,
        CancellationToken cancellationToken)
    {
        return Created(string.Empty, await useCase.Execute(request, cancellationToken));
    }

    [HttpPut]
    [Route("cashbox/{accountId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCashbox(
        [FromServices] AssignCashboxTerminalUseCase useCase,
        [FromRoute] Guid accountId,
        [FromBody] RequestAssignCashboxTerminalJson request,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(accountId, request, cancellationToken));
    }
}
