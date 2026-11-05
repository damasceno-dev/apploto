using Microsoft.AspNetCore.Mvc;
using server.Application.Services.Idempotency;
using server.Application.UseCases.CardSettlements.Create;
using server.Application.UseCases.CardSettlements.FeeEvidence;
using server.Application.UseCases.CardProviders.Create;
using server.Application.UseCases.CardProviders.List;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[ApiController]
public class CardSettlementController : ControllerBase
{
    [HttpPost]
    [Route("card-provider")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCardProviderJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProvider(
        [FromServices] CreateCardProviderUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestCardProviderJson request,
        CancellationToken cancellationToken) => Created(string.Empty, await useCase.Execute(request, idempotencyKey, cancellationToken));

    [HttpPost]
    [Route("card-settlement")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCardSettlementJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Settle(
        [FromServices] CardSettlementUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestCardSettlementJson request,
        CancellationToken cancellationToken) => Created(string.Empty, await useCase.Execute(request, idempotencyKey, cancellationToken));
    [HttpGet]
    [Route("card-provider")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<ResponseCardProviderJson>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListProviders(
        [FromServices] ListCardProvidersUseCase useCase,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(cancellationToken));
    [HttpPost]
    [Route("card-settlement/fee-evidence")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankReceiptJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordFeeEvidence(
        [FromServices] RecordCardFeeEvidenceUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestCardFeeEvidenceJson request,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(request, idempotencyKey, cancellationToken));

}
