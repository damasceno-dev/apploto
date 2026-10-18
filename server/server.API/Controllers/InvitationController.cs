using server.Application.UseCases.Invitations.Issue;
using server.Application.UseCases.Invitations.List;
using server.Application.UseCases.Invitations.Reissue;
using server.Application.UseCases.Invitations.Preview;
using server.Application.UseCases.Invitations.Decline;
using server.Application.UseCases.Invitations.Accept;
using server.Application.UseCases.Invitations.Register;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[ApiController]
[Route("invitation")]
public sealed class InvitationController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthorize(Role.Admin, Role.Manager)]
    [ProducesResponseType(typeof(ResponseInvitationJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Issue([FromServices] IssueInvitationUseCase useCase, [FromBody] RequestIssueInvitationJson request)
    {
        return Created(string.Empty, await useCase.Execute(request));
    }

    [HttpGet]
    [Route("")]
    [TokenAuthorize(Role.Admin, Role.Manager)]
    [ProducesResponseType(typeof(ResponseListInvitationsJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    public async Task<IActionResult> List([FromServices] ListInvitationsUseCase useCase, [FromQuery] RequestListInvitationsJson request)
    {
        return Ok(await useCase.Execute(request));
    }

    [HttpPost]
    [Route("{id:guid}/reissue")]
    [TokenAuthorize(Role.Admin, Role.Manager)]
    [ProducesResponseType(typeof(ResponseInvitationJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    public async Task<IActionResult> Reissue([FromServices] ReissueInvitationUseCase useCase, [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RequestReissueInvitationJson? request = null)
    {
        return Ok(await useCase.Execute(id, request));
    }

    [HttpPost]
    [Route("preview")]
    [EnableRateLimiting("capability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseInvitationPreviewJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    [ProducesResponseType(typeof(ResponseErrorJson), 429)]
    public async Task<IActionResult> Preview([FromServices] PreviewInvitationUseCase useCase, [FromBody] RequestInvitationTokenJson request)
    {
        return Ok(await useCase.Execute(request));
    }

    [HttpPost]
    [Route("decline")]
    [EnableRateLimiting("capability")]
    [AllowAnonymous]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    [ProducesResponseType(typeof(ResponseErrorJson), 429)]
    public async Task<IActionResult> Decline([FromServices] DeclineInvitationUseCase useCase, [FromBody] RequestInvitationTokenJson request)
    {
        await useCase.Execute(request);
        return NoContent();
    }

    [HttpPost]
    [Route("accept")]
    [TokenAuthenticate]
    [ProducesResponseType(typeof(ResponseAcceptInvitationJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Accept([FromServices] AcceptInvitationUseCase useCase, [FromBody] RequestInvitationTokenJson request)
    {
        return Ok(await useCase.Execute(request));
    }

    [HttpPost]
    [Route("register")]
    [EnableRateLimiting("capability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseUserLoginJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    [ProducesResponseType(typeof(ResponseErrorJson), 429)]
    public async Task<IActionResult> Register([FromServices] RegisterInvitationUseCase useCase, [FromBody] RequestRegisterInvitationJson request)
    {
        return Created(string.Empty, await useCase.Execute(request));
    }

}
