using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.GoLive.Imports;
using server.Application.Services.Idempotency;
using server.Application.UseCases.GoLive.Openings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;
using server.Headers;

namespace server.Controllers;

[ApiController]
[Route("go-live")]
[TokenAuthorize(Role.Manager, Role.Admin)]
public class GoLiveController : ControllerBase
{
    [HttpPost("openings")]
    [ProducesResponseType(typeof(ResponseGoLiveOpeningJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Opening(
        [FromServices] RecordGoLiveOpeningUseCase useCase,
        [FromBody] RequestGoLiveOpeningJson request,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        CancellationToken ct)
    {
        var response = await useCase.Execute(request, key, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created(string.Empty, response);
    }

    [HttpPost("openings/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ResponseGoLiveOpeningJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOpening(
        [FromServices] CancelGoLiveOpeningUseCase useCase,
        [FromRoute] Guid id,
        [FromBody] RequestCancelGoLiveOpeningJson request,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        CancellationToken ct)
    {
        var response = await useCase.Execute(id, request, key, EntityTagHeader.ParseRequired(ifMatch), ct);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }
    [HttpGet("imports")]
    [ProducesResponseType(typeof(IReadOnlyList<ResponseGoLiveImportJson>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Imports(
        [FromServices] GoLiveImportUseCase useCase,
        CancellationToken ct) => Ok(await useCase.List(ct));

    [HttpGet("imports/{id:guid}")]
    [ProducesResponseType(typeof(ResponseGoLiveImportJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Import(
        [FromServices] GoLiveImportUseCase useCase,
        [FromRoute] Guid id,
        CancellationToken ct) => Ok(await useCase.Get(id, ct));

    [HttpPost("imports")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(ResponseGoLiveImportJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upload(
        [FromServices] GoLiveImportUseCase useCase,
        [FromBody] RequestGoLiveImportJson request,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        CancellationToken ct) => Ok(await useCase.Upload(request, key, ct));

    [HttpPost("imports/{id:guid}/mapping")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(ResponseGoLiveImportJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Mapping(
        [FromServices] GoLiveImportUseCase useCase,
        [FromRoute] Guid id,
        [FromBody] RequestGoLiveMappingJson request,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        CancellationToken ct) => Ok(await useCase.Map(id, request, key, ct));

    [HttpPost("imports/{id:guid}/preview")]
    [ProducesResponseType(typeof(ResponseGoLiveImportJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Preview(
        [FromServices] GoLiveImportUseCase useCase,
        [FromRoute] Guid id,
        [FromBody] RequestGoLiveMappingJson request,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        CancellationToken ct) => Ok(await useCase.Preview(id, request.ExpectedRevision, key, ct));

    [HttpPost("imports/{id:guid}/activate")]
    [ProducesResponseType(typeof(ResponseGoLiveImportJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(
        [FromServices] GoLiveImportUseCase useCase,
        [FromRoute] Guid id,
        [FromBody] RequestActivateGoLiveImportJson request,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        CancellationToken ct) => Ok(await useCase.Activate(id, request, key, ct));

    [HttpGet("openings")]
    [ProducesResponseType(typeof(IReadOnlyList<ResponseGoLiveOpeningJson>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Openings([FromServices] ListGoLiveOpeningsUseCase useCase, CancellationToken ct) => Ok(await useCase.Execute(ct));

    [HttpPost("openings/{id:guid}/amend")]
    [ProducesResponseType(typeof(ResponseGoLiveOpeningJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AmendOpening([FromServices] AmendGoLiveOpeningUseCase useCase, [FromRoute] Guid id,
        [FromBody] RequestAmendGoLiveOpeningJson request, [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, CancellationToken ct)
    {
        var response = await useCase.Execute(id, request, EntityTagHeader.ParseRequired(ifMatch), key, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }
}
