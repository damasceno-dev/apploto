using server.Application.UseCases.OperatorLinkRequests.Create;
using server.Application.UseCases.OperatorLinkRequests.Own;
using server.Application.UseCases.OperatorLinkRequests.List;
using server.Application.UseCases.OperatorLinkRequests.Resolve;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[ApiController]
[Route("operator-link-request")]
public sealed class OperatorLinkRequestController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthorize(Role.Member)]
    [ProducesResponseType(typeof(ResponseOperatorLinkRequestJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Create([FromServices] CreateOperatorLinkRequestUseCase useCase)
    {
        return Ok(await useCase.Execute());
    }

    [HttpGet]
    [Route("self")]
    [TokenAuthorize(Role.Member)]
    [ProducesResponseType(typeof(ResponseOwnOperatorLinkRequestJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    public async Task<IActionResult> Own([FromServices] GetOwnOperatorLinkRequestUseCase useCase)
    {
        return Ok(await useCase.Execute());
    }

    [HttpGet]
    [Route("")]
    [TokenAuthorize(Role.Admin, Role.Manager)]
    [ProducesResponseType(typeof(ResponseListOperatorLinkRequestsJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    public async Task<IActionResult> List([FromServices] ListOperatorLinkRequestsUseCase useCase, [FromQuery] RequestListOperatorLinkRequestsJson request)
    {
        return Ok(await useCase.Execute(request));
    }

    [HttpPost]
    [Route("{id:guid}/resolve")]
    [TokenAuthorize(Role.Admin, Role.Manager)]
    [ProducesResponseType(typeof(ResponseOperatorLinkRequestJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> Resolve([FromServices] ResolveOperatorLinkRequestUseCase useCase, [FromRoute] Guid id)
    {
        return Ok(await useCase.Execute(id));
    }

}
