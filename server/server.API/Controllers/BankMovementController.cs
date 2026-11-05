using Microsoft.AspNetCore.Mvc;
using server.Application.Services.Idempotency;
using server.Application.UseCases.BankMovements.Create;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("bank-movement")]
[ApiController]
public class BankMovementController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankMovementJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Import(
        [FromServices] BankMovementUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestBankMovementJson request,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(request, idempotencyKey, cancellationToken));

}
