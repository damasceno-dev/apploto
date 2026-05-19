using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.TransactionTypes.Create;
using server.Application.UseCases.TransactionTypes.Deactivate;
using server.Application.UseCases.TransactionTypes.Get;
using server.Application.UseCases.TransactionTypes.List;
using server.Application.UseCases.TransactionTypes.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("transaction-type")]
[ApiController]
public class TransactionTypeController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseTransactionTypeJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromServices] CreateTransactionTypeUseCase useCase,
        [FromBody] RequestCreateTransactionTypeJson request)
    {
        var response = await useCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListTransactionTypesJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromServices] ListTransactionTypesUseCase useCase)
    {
        var response = await useCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("{transactionTypeId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTransactionTypeJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetTransactionTypeUseCase useCase,
        [FromRoute] Guid transactionTypeId)
    {
        var response = await useCase.Execute(transactionTypeId);
        return Ok(response);
    }

    [HttpPut]
    [Route("{transactionTypeId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseTransactionTypeJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateTransactionTypeUseCase useCase,
        [FromRoute] Guid transactionTypeId,
        [FromBody] RequestUpdateTransactionTypeJson request)
    {
        var response = await useCase.Execute(transactionTypeId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{transactionTypeId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateTransactionTypeUseCase useCase,
        [FromRoute] Guid transactionTypeId)
    {
        await useCase.Execute(transactionTypeId);
        return NoContent();
    }
}
