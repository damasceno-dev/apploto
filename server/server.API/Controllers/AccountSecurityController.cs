using server.Application.UseCases.Users.GetProfile;
using server.Application.UseCases.Users.UpdateProfile;
using server.Application.UseCases.Users.ChangePassword;
using server.Application.UseCases.Users.Logout;
using server.Application.UseCases.Users.ForgotPassword;
using server.Application.UseCases.Users.ResetPassword;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[ApiController]
[Route("user")]
public sealed class AccountSecurityController : ControllerBase
{
    [HttpGet]
    [Route("profile")]
    [TokenAuthenticate]
    [ProducesResponseType(typeof(ResponseUserProfileJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    public async Task<IActionResult> Profile([FromServices] GetUserProfileUseCase useCase)
    {
        return Ok(await useCase.Execute());
    }

    [HttpPut]
    [Route("profile")]
    [TokenAuthenticate]
    [ProducesResponseType(typeof(ResponseUserProfileJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> UpdateProfile([FromServices] UpdateUserProfileUseCase useCase, [FromBody] RequestUpdateProfileJson request)
    {
        return Ok(await useCase.Execute(request));
    }

    [HttpPost]
    [Route("change-password")]
    [TokenAuthenticate]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ChangePassword([FromServices] ChangeUserPasswordUseCase useCase, [FromBody] RequestChangePasswordJson request)
    {
        await useCase.Execute(request);
        return NoContent();
    }

    [HttpPost]
    [Route("logout")]
    [TokenAuthenticate]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Logout([FromServices] LogoutUserUseCase useCase, [FromBody] RequestLogoutJson request)
    {
        await useCase.Execute(request);
        return NoContent();
    }

    [HttpPost]
    [Route("forgot-password")]
    [EnableRateLimiting("recovery")]
    [AllowAnonymous]
    [ProducesResponseType(202)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ForgotPassword([FromServices] ForgotUserPasswordUseCase useCase, [FromBody] RequestForgotPasswordJson request)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        await useCase.Execute(request);
        var remaining = TimeSpan.FromMilliseconds(200) - System.Diagnostics.Stopwatch.GetElapsedTime(start);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, HttpContext.RequestAborted);
        return Accepted();
    }

    [HttpPost]
    [Route("reset-password")]
    [EnableRateLimiting("capability")]
    [AllowAnonymous]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    [ProducesResponseType(typeof(ResponseErrorJson), 429)]
    public async Task<IActionResult> ResetPassword([FromServices] ResetUserPasswordUseCase useCase, [FromBody] RequestResetPasswordJson request)
    {
        await useCase.Execute(request);
        return NoContent();
    }

}
