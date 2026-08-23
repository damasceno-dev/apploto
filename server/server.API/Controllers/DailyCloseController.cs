using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.DailyCloses.Approve;
using server.Application.UseCases.DailyCloses.Get;
using server.Application.UseCases.DailyCloses.List;
using server.Application.UseCases.DailyCloses.Open;
using server.Application.UseCases.DailyCloses.PutItems;
using server.Application.UseCases.DailyCloses.Reject;
using server.Application.UseCases.DailyCloses.Recall;
using server.Application.UseCases.DailyCloses.Reopen;
using server.Application.UseCases.DailyCloses.Review;
using server.Application.UseCases.DailyCloses.Submit;
using server.Application.UseCases.DailyCloses.VariancePreview;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Filters;
using server.Domain.Entities.Enums;
using server.Headers;

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
        [FromBody] RequestOpenDailyCloseJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListDailyClosesJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromServices] ListDailyClosesUseCase useCase,
        [FromQuery] RequestListDailyClosesJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    [Route("{dailyCloseId:guid}/items")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponsePutDailyCloseItemsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutItems(
        [FromServices] PutDailyCloseItemsUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestPutDailyCloseItemsJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(
            dailyCloseId,
            request,
            EntityTagHeader.ParseRequired(ifMatch),
            cancellationToken);
        EntityTagHeader.Set(Response, response.DailyClose.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{dailyCloseId:guid}/submit")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromServices] SubmitDailyCloseUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{dailyCloseId:guid}/approve")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        [FromServices] ApproveDailyCloseUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{dailyCloseId:guid}/reject")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        [FromServices] RejectDailyCloseUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        [FromBody] RequestRejectDailyCloseJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, request, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    //after a member has submitted the dailyclose, he can recall to include a forgotten transaction for example.
    [HttpPost]
    [Route("{dailyCloseId:guid}/recall")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recall(
        [FromServices] RecallDailyCloseUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    //after the manager has approved, but some transaction came days later, he can reopen the dailyclose to include a
    //forgotten transaction for example
    [HttpPost]
    [Route("{dailyCloseId:guid}/reopen")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reopen(
        [FromServices] ReopenDailyCloseUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
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
        [FromRoute] Guid dailyCloseId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpGet]
    [Route("{dailyCloseId:guid}/review")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseReviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(
        [FromServices] GetDailyCloseReviewUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{dailyCloseId:guid}/variance-preview")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseVariancePreviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VariancePreview(
        [FromServices] PreviewDailyCloseVarianceUseCase useCase,
        [FromRoute] Guid dailyCloseId,
        [FromBody] RequestDailyCloseVariancePreviewJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(dailyCloseId, request, cancellationToken);
        return Ok(response);
    }
}
