using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Transactions.Cancel;
using server.Application.UseCases.Transactions.Create;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Application.UseCases.Transactions.CreatePreview;
using server.Application.UseCases.Transactions.EditPreview;
using server.Application.UseCases.Transactions.Finalize;
using server.Application.UseCases.Transactions.Get;
using server.Application.UseCases.Transactions.InstallmentPreview;
using server.Application.UseCases.Transactions.List;
using server.Application.UseCases.Transactions.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class TransactionController : ControllerBase
{
    [HttpGet]
    [Route("{transactionId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTransactionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetTransactionUseCase useCase,
        [FromRoute] Guid transactionId)
    {
        var response = await useCase.Execute(transactionId);
        return Ok(response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListTransactionsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromServices] ListTransactionsUseCase useCase,
        [FromQuery] RequestListTransactionsJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpPost]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseCreateTransactionJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromServices] CreateTransactionUseCase createTransactionUseCase,
        [FromBody] RequestCreateTransactionJson request,
        CancellationToken cancellationToken)
    {
        var response = await createTransactionUseCase.Execute(request, cancellationToken);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("preview")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseCreateTransactionPreviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePreview(
        [FromServices] PreviewCreateTransactionUseCase previewCreateTransactionUseCase,
        [FromQuery] DateTime? asOfDate,
        [FromBody] RequestCreateTransactionJson request,
        CancellationToken cancellationToken)
    {
        var response = await previewCreateTransactionUseCase.Execute(request, asOfDate, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Route("installment")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseCreateTransactionInstallmentJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInstallment(
        [FromServices] CreateTransactionInstallmentUseCase createTransactionInstallmentUseCase,
        [FromBody] RequestCreateTransactionInstallmentJson request,
        CancellationToken cancellationToken)
    {
        var response = await createTransactionInstallmentUseCase.Execute(request, cancellationToken);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("installment/preview")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseInstallmentPreviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewInstallment(
        [FromServices] PreviewInstallmentPlanUseCase previewInstallmentPlanUseCase,
        [FromQuery] DateTime? asOfDate,
        [FromBody] RequestCreateTransactionInstallmentJson request,
        CancellationToken cancellationToken)
    {
        var response = await previewInstallmentPlanUseCase.Execute(request, asOfDate, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Route("{transactionId:guid}/finalize")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTransactionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Finalize(
        [FromServices] FinalizeTransactionUseCase finalizeTransactionUseCase,
        [FromRoute] Guid transactionId,
        CancellationToken cancellationToken)
    {
        var response = await finalizeTransactionUseCase.Execute(transactionId, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Route("{transactionId:guid}/cancel")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTransactionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        [FromServices] CancelTransactionUseCase cancelTransactionUseCase,
        [FromRoute] Guid transactionId,
        [FromBody] RequestCancelTransactionJson request,
        CancellationToken cancellationToken)
    {
        var response = await cancelTransactionUseCase.Execute(transactionId, request, cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    [Route("{transactionId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTransactionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateTransactionUseCase updateTransactionUseCase,
        [FromRoute] Guid transactionId,
        [FromBody] RequestUpdateTransactionJson request,
        CancellationToken cancellationToken)
    {
        var response = await updateTransactionUseCase.Execute(transactionId, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Route("{transactionId:guid}/edit-preview")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseEditTransactionPreviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditPreview(
        [FromServices] PreviewEditTransactionUseCase previewEditTransactionUseCase,
        [FromRoute] Guid transactionId,
        [FromBody] RequestUpdateTransactionJson request,
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var response = await previewEditTransactionUseCase.Execute(transactionId, request, asOfDate, cancellationToken);
        return Ok(response);
    }
}
