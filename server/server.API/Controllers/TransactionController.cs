using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Transactions.Create;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class TransactionController : ControllerBase
{
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
}
