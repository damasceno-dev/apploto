using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.DailyCloses.Approve;
using server.Application.UseCases.DailyCloses.Get;
using server.Application.UseCases.DailyCloses.History;
using server.Application.UseCases.DailyCloses.List;
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
using server.Application.Services.Idempotency;
using server.Application.UseCases.Sessions.Attribution;
using server.Application.UseCases.Sessions.CustodyException;
using server.Application.UseCases.Sessions.Declare;
using server.Application.UseCases.Sessions.Finish;
using server.Application.UseCases.Sessions.Handover;
using server.Application.UseCases.Sessions.OpeningCount;
using server.Application.UseCases.Sessions.OpeningProposal;
using server.Application.UseCases.Sessions.Rearrange;
using server.Application.UseCases.Sessions.Start;

namespace server.Controllers;

[Route("session")]
[ApiController]
public class SessionController : ControllerBase
{
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
    [Route("{sessionId:guid}/items")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponsePutDailyCloseItemsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutItems(
        [FromServices] PutDailyCloseItemsUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestPutDailyCloseItemsJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(
            sessionId,
            request,
            EntityTagHeader.ParseRequired(ifMatch),
            cancellationToken);
        EntityTagHeader.Set(Response, response.DailyClose.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/submit")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromServices] SubmitDailyCloseUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/approve")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        [FromServices] ApproveDailyCloseUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/reject")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        [FromServices] RejectDailyCloseUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromBody] RequestRejectDailyCloseJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, request, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    //after a member has submitted the session, he can recall to include a forgotten transaction for example.
    [HttpPost]
    [Route("{sessionId:guid}/recall")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recall(
        [FromServices] RecallDailyCloseUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    //after the manager has approved, but some transaction came days later, he can reopen the session to include a
    //forgotten transaction for example
    [HttpPost]
    [Route("{sessionId:guid}/reopen")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reopen(
        [FromServices] ReopenDailyCloseUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpGet]
    [Route("{sessionId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetDailyCloseUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpGet]
    [Route("{sessionId:guid}/review")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseReviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(
        [FromServices] GetDailyCloseReviewUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpGet]
    [Route("{sessionId:guid}/history")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseHistoryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(
        [FromServices] GetDailyCloseHistoryUseCase useCase,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/variance-preview")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseVariancePreviewJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VariancePreview(
        [FromServices] PreviewDailyCloseVarianceUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromBody] RequestDailyCloseVariancePreviewJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, request, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    [Route("opening-proposal")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseSessionOpeningProposalJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpeningProposal(
        [FromServices] GetSessionOpeningProposalUseCase useCase,
        [FromQuery] Guid accountId,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(accountId, cancellationToken));
    }

    [HttpPost]
    [Route("start")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseStartSessionJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        [FromServices] StartSessionUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestStartSessionJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, idempotencyKey, cancellationToken);
        EntityTagHeader.Set(Response, response.Session.Version);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/handover")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseSessionHandoverJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OfferHandover(
        [FromServices] OfferSessionHandoverUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestOfferSessionHandoverJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(
            sessionId,
            request,
            EntityTagHeader.ParseRequired(ifMatch),
            idempotencyKey,
            cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("handover/{handoverId:guid}/accept")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseAcceptSessionHandoverJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptHandover(
        [FromServices] AcceptSessionHandoverUseCase useCase,
        [FromRoute] Guid handoverId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestAcceptSessionHandoverJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(
            handoverId,
            request,
            EntityTagHeader.ParseRequired(ifMatch),
            idempotencyKey,
            cancellationToken);
        EntityTagHeader.Set(Response, response.Handover.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("handover/{handoverId:guid}/dispute")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseSessionHandoverJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DisputeHandover(
        [FromServices] ResolveSessionHandoverOfferUseCase useCase,
        [FromRoute] Guid handoverId,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestSessionReasonJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Dispute(handoverId, request, EntityTagHeader.ParseRequired(ifMatch), cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("handover/{handoverId:guid}/withdraw")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseSessionHandoverJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawHandover(
        [FromServices] ResolveSessionHandoverOfferUseCase useCase,
        [FromRoute] Guid handoverId,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestSessionReasonJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Withdraw(handoverId, request, EntityTagHeader.ParseRequired(ifMatch), cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/finish")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseDailyCloseJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Finish(
        [FromServices] FinishSessionUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestFinishSessionJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(
            sessionId,
            request,
            EntityTagHeader.ParseRequired(ifMatch),
            idempotencyKey,
            cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/custody-exception")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseResolveCustodyExceptionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveCustodyException(
        [FromServices] ResolveCustodyExceptionUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestResolveCustodyExceptionJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(
            sessionId,
            request,
            EntityTagHeader.ParseRequired(ifMatch),
            idempotencyKey,
            cancellationToken);
        EntityTagHeader.Set(Response, response.OutgoingSession.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("declare")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseSessionChainChangeJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Declare(
        [FromServices] DeclarePastSessionUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestDeclarePastSessionJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, idempotencyKey, cancellationToken);
        EntityTagHeader.Set(Response, response.Session.Version);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("{sessionId:guid}/rearrange")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseSessionChainChangeJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rearrange(
        [FromServices] RearrangePastSessionUseCase useCase,
        [FromRoute] Guid sessionId,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestRearrangePastSessionJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(sessionId, request, EntityTagHeader.ParseRequired(ifMatch), cancellationToken);
        EntityTagHeader.Set(Response, response.Session.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("opening-discrepancy/{discrepancyId:guid}/attribution")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOpeningDiscrepancyJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AttributeOpeningDiscrepancy(
        [FromServices] AttributeOpeningDiscrepancyUseCase useCase,
        [FromRoute] Guid discrepancyId,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch,
        [FromBody] RequestAttributeOpeningDiscrepancyJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(discrepancyId, request, EntityTagHeader.ParseRequired(ifMatch), cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpPost]
    [Route("opening-count")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOpeningCountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordOpeningCount(
        [FromServices] RecordOpeningCountUseCase useCase,
        [FromBody] RequestRecordOpeningCountJson request,
        CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        EntityTagHeader.Set(Response, response.Version);
        return Ok(response);
    }

    [HttpGet]
    [Route("opening-count")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOpeningCountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOpeningCount(
        [FromServices] GetOpeningCountUseCase useCase,
        [FromQuery] Guid accountId,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.Execute(accountId, cancellationToken));
    }
}
