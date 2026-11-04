using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Receivables.Get;
using server.Application.UseCases.Receivables.GetBankReceipt;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

/// <summary>Receivable lifecycle reads (M9.7 Phases 1–2): events, cutoff projection and pending bank receipts.</summary>
[ApiController]
public class ReceivableController : ControllerBase
{
    [HttpGet]
    [Route("receivable/{transactionId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseReceivableJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetReceivableUseCase useCase,
        [FromRoute] Guid transactionId,
        [FromQuery] RequestReceivableJson request,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(transactionId, request, cancellationToken));
    }

    [HttpGet]
    [Route("bank-receipt/{receiptId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankReceiptJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBankReceipt(
        [FromServices] GetBankReceiptUseCase useCase,
        [FromRoute] Guid receiptId,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(receiptId, cancellationToken));
    }
}
