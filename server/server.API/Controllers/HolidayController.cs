using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Holidays.Create;
using server.Application.UseCases.Holidays.Deactivate;
using server.Application.UseCases.Holidays.List;
using server.Application.UseCases.Holidays.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Filters;

namespace server.Controllers;

[Route("holiday")]
[ApiController]
public class HolidayController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseCreateHolidaysJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromServices] CreateHolidaysUseCase useCase,
        [FromBody] RequestCreateHolidaysJson request)
    {
        var response = await useCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListHolidaysJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromServices] ListHolidaysUseCase useCase,
        [FromQuery] RequestListHolidaysJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpPut]
    [Route("{holidayId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseHolidayJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateHolidayUseCase useCase,
        [FromRoute] Guid holidayId,
        [FromBody] RequestUpdateHolidayJson request)
    {
        var response = await useCase.Execute(holidayId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{holidayId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateHolidayUseCase useCase,
        [FromRoute] Guid holidayId)
    {
        await useCase.Execute(holidayId);
        return NoContent();
    }
}
