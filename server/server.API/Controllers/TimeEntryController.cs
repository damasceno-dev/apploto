using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.TimeEntries.AddSegment;
using server.Application.UseCases.TimeEntries.Deactivate;
using server.Application.UseCases.TimeEntries.DeactivateSegment;
using server.Application.UseCases.TimeEntries.Get;
using server.Application.UseCases.TimeEntries.List;
using server.Application.UseCases.TimeEntries.UpdateSegment;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("timeentry")]
[ApiController]
public class TimeEntryController : ControllerBase
{
    [HttpPut]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTimeEntryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upsert(
        [FromServices] UpsertTimeEntryUseCase useCase,
        [FromBody] RequestUpsertTimeEntryJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    [Route("{timeEntryId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTimeEntryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetTimeEntryUseCase useCase,
        [FromRoute] Guid timeEntryId)
    {
        var response = await useCase.Execute(timeEntryId);
        return Ok(response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListTimeEntriesJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromServices] ListTimeEntriesUseCase useCase,
        [FromQuery] RequestListTimeEntriesJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpPost]
    [Route("{timeEntryId:guid}/segment")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseTimeEntryJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSegment(
        [FromServices] AddTimeEntrySegmentUseCase useCase,
        [FromRoute] Guid timeEntryId,
        [FromBody] RequestAddTimeEntrySegmentJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(timeEntryId, request, cancellationToken);
        var location = response.Segments
            .OrderByDescending(segment => segment.CreatedAt)
            .ThenByDescending(segment => segment.Id)
            .Select(segment => $"/timeentry/segment/{segment.Id}")
            .FirstOrDefault() ?? $"/timeentry/{timeEntryId}";

        return Created(location, response);
    }

    [HttpPut]
    [Route("segment/{segmentId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseTimeEntryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSegment(
        [FromServices] UpdateTimeEntrySegmentUseCase useCase,
        [FromRoute] Guid segmentId,
        [FromBody] RequestUpdateTimeEntrySegmentJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(segmentId, request, cancellationToken);
        return Ok(response);
    }

    [HttpDelete]
    [Route("segment/{segmentId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseTimeEntryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateSegment(
        [FromServices] DeactivateTimeEntrySegmentUseCase useCase,
        [FromRoute] Guid segmentId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(segmentId, cancellationToken);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{timeEntryId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTimeEntryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateTimeEntryUseCase useCase,
        [FromRoute] Guid timeEntryId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(timeEntryId, cancellationToken);
        return Ok(response);
    }
}
