using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Settings.Get;
using server.Application.UseCases.Settings.LockMonth;
using server.Application.UseCases.Settings.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("setting")]
[ApiController]
public class SettingController : ControllerBase
{
    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseSettingJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromServices] GetSettingUseCase useCase)
    {
        var response = await useCase.Execute();
        return Ok(response);
    }

    [HttpPut]
    [Route("")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseSettingJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateSettingUseCase useCase,
        [FromBody] RequestUpdateSettingJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpPost]
    [Route("lock-month")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseSettingJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LockMonth(
        [FromServices] LockSettingMonthUseCase useCase,
        [FromBody] RequestLockSettingMonthJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return Ok(response);
    }
}
