using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Transactions.Cancel;
using server.Application.UseCases.Transactions.Create;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Application.UseCases.Transactions.Finalize;
using server.Application.UseCases.Transactions.Get;
using server.Application.UseCases.Transactions.InstallmentPreview;
using server.Application.UseCases.Transactions.List;
using server.Application.UseCases.Transactions.Update;
using server.Communication.Requests;
using server.Communication.Responses;
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
        [FromBody] RequestCreateTransactionJson request)
    {
        var response = await createTransactionUseCase.Execute(request);
        return Created(string.Empty, response);
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
        [FromBody] RequestCreateTransactionInstallmentJson request)
    {
        var response = await createTransactionInstallmentUseCase.Execute(request);
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
        [FromBody] RequestCreateTransactionInstallmentJson request)
    {
        var response = await previewInstallmentPlanUseCase.Execute(request);
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
        [FromRoute] Guid transactionId)
    {
        var response = await finalizeTransactionUseCase.Execute(transactionId);
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
        [FromBody] RequestCancelTransactionJson request)
    {
        var response = await cancelTransactionUseCase.Execute(transactionId, request);
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
        [FromBody] RequestUpdateTransactionJson request)
    {
        var response = await updateTransactionUseCase.Execute(transactionId, request);
        return Ok(response);
    }
}
