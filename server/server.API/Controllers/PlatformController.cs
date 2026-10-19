using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Platform.OperateBranch;
using server.Application.UseCases.Platform.ReadOnboarding;
using server.Application.UseCases.Platform.ReissueOwnerInvitation;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Filters;

namespace server.Controllers;

[ApiController]
[Route("platform/onboarding")]
public sealed class PlatformController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenPlatformOperator]
    [ProducesResponseType(typeof(ResponseOperatedOnboardingJson), 201)]
    [ProducesResponseType(typeof(ResponseOperatedOnboardingJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Create([FromServices] OperateBranchUseCase useCase, [FromBody] RequestOperateBranchJson request)
    {
        var result = await useCase.Execute(request);
        return result.Created ? Created(string.Empty, result.Response) : Ok(result.Response);
    }

    [HttpGet]
    [Route("{requestId:guid}")]
    [TokenPlatformOperator]
    [ProducesResponseType(typeof(ResponseOperatedOnboardingJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    public async Task<IActionResult> Get([FromServices] ReadOperatedOnboardingUseCase useCase, Guid requestId) =>
        Ok(await useCase.Execute(requestId));

    [HttpPost]
    [Route("{requestId:guid}/reissue-owner-invitation")]
    [TokenPlatformOperator]
    [ProducesResponseType(typeof(ResponseOperatedOnboardingJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Reissue([FromServices] ReissueOwnerInvitationUseCase useCase, Guid requestId) =>
        Ok(await useCase.Execute(requestId));
}
