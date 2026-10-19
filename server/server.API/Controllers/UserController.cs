using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using server.Application.UseCases.Users.Login;
using server.Application.UseCases.Users.Google;
using server.Application.UseCases.Users.RenewToken;
using server.Communication.Requests;
using server.Communication.Responses;

namespace server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ResponseUserLoginJson), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Login([FromServices] UserLoginUseCase userLoginUseCase, [FromBody] RequestUserLoginJson request)
        {
            var response = await userLoginUseCase.Execute(request);
            return Ok(response);
        }

        [HttpPost]
        [Route("google/challenge")]
        [EnableRateLimiting("capability")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ResponseGoogleChallengeJson), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GoogleChallenge([FromServices] CreateGoogleChallengeUseCase useCase) =>
            Ok(await useCase.Execute());

        [HttpPost]
        [Route("google/sign-in")]
        [EnableRateLimiting("capability")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ResponseUserLoginJson), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GoogleSignIn([FromServices] GoogleSignInUseCase useCase,
            [FromBody] RequestGoogleSignInJson request, CancellationToken ct) => Ok(await useCase.Execute(request, ct));

        [HttpPost]
        [Route("renew-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ResponseTokenJson), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RenewToken([FromServices] UserRenewTokenUseCase userRenewTokenUseCase, [FromBody] RequestUserRenewTokenJson request)
        {
            var response = await userRenewTokenUseCase.Execute(request);
            return Ok(response);
        }
    }
}
