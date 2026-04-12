using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Users.Login;
using server.Application.UseCases.Users.Register;
using server.Application.UseCases.Users.RenewToken;
using server.Communication.Requests;

namespace server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpPost]
        [Route("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromServices]UserRegisterUseCase userRegisterUseCase, [FromBody] RequestUserRegisterJson request)
        {
            var response = await userRegisterUseCase.Execute(request);
            return Created(string.Empty, response);
        }

        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromServices] UserLoginUseCase userLoginUseCase, [FromBody] RequestUserLoginJson request)
        {
            var response = await userLoginUseCase.Execute(request);
            return Ok(response);
        }

        [HttpPost]
        [Route("renew-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RenewToken([FromServices] UserRenewTokenUseCase userRenewTokenUseCase, [FromBody] RequestUserRenewTokenJson request)
        {
            var response = await userRenewTokenUseCase.Execute(request);
            return Ok(response);
        }
    }
}
