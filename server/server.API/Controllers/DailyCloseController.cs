using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.DailyCloses.Get;
using server.Application.UseCases.DailyCloses.Open;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Filters;

namespace server.Controllers;

[Route("dailyclose")]
[ApiController]
public class DailyCloseController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Open(
        [FromServices] OpenDailyCloseUseCase useCase,
        [FromBody] RequestOpenDailyCloseJson request)
    {
        var response = await useCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("{dailyCloseId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetDailyCloseUseCase useCase,
        [FromRoute] Guid dailyCloseId)
    {
        var response = await useCase.Execute(dailyCloseId);
        return Ok(response);
    }
}
