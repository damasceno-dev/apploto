using Microsoft.AspNetCore.Mvc;
using server.Application.Services.Idempotency;
using server.Application.UseCases.Cheques.ConvertToFiado;
using server.Application.UseCases.Cheques.Custody;
using server.Application.UseCases.Cheques.Deposit;
using server.Application.UseCases.Cheques.Represent;
using server.Application.UseCases.Cheques.Return;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

/// <summary>
/// Cheque lifecycle commands (M9.7 Phase 2). Each appends settlement events and never edits the cheque row; each
/// requires an <c>Idempotency-Key</c> and the receivable version(s) read.
/// </summary>
[Route("cheque")]
[ApiController]
public class ChequeController : ControllerBase
{
    [HttpPost]
    [Route("custody")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseChequeOperationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Custody(
        [FromServices] ChequeCustodyUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestChequeCustodyJson request,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(request, idempotencyKey, cancellationToken));
    }

    [HttpPost]
    [Route("deposit")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseChequeOperationJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deposit(
        [FromServices] ChequeDepositUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestChequeDepositJson request,
        CancellationToken cancellationToken)
    {
        return Created(string.Empty, await useCase.Execute(request, idempotencyKey, cancellationToken));
    }

    [HttpPost]
    [Route("{transactionId:guid}/return")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseChequeOperationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Return(
        [FromServices] ChequeReturnUseCase useCase,
        [FromRoute] Guid transactionId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestChequeReturnJson request,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(transactionId, request, idempotencyKey, cancellationToken));
    }

    [HttpPost]
    [Route("{transactionId:guid}/represent")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseChequeOperationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Represent(
        [FromServices] ChequeRepresentUseCase useCase,
        [FromRoute] Guid transactionId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestChequeRepresentJson request,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(transactionId, request, idempotencyKey, cancellationToken));
    }

    [HttpPost]
    [Route("{transactionId:guid}/convert-to-fiado")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseChequeOperationJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConvertToFiado(
        [FromServices] ChequeConvertToFiadoUseCase useCase,
        [FromRoute] Guid transactionId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestChequeConvertToFiadoJson request,
        CancellationToken cancellationToken)
    {
        return Created(string.Empty, await useCase.Execute(transactionId, request, idempotencyKey, cancellationToken));
    }
}
