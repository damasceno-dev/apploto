using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.TimeEntries.Deactivate;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Requests;
using server.Communication.Responses;
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
        [FromBody] RequestUpsertTimeEntryJson request)
    {
        var response = await useCase.Execute(request);
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
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateTimeEntryUseCase useCase,
        [FromRoute] Guid timeEntryId)
    {
        var response = await useCase.Execute(timeEntryId);
        return Ok(response);
    }
}
