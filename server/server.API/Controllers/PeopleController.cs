using Microsoft.AspNetCore.Mvc;
using server.Application.Services.Idempotency;
using server.Application.UseCases.People;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using server.Filters;
using server.Headers;
namespace server.Controllers;

[ApiController]
[Route("people")]
[TokenAuthorize(Role.Manager, Role.Admin)]
public class PeopleController : ControllerBase
{
    [HttpPost]
    [Route("pay-category")]
    [ProducesResponseType(typeof(ResponsePayCategoryJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreatePayCategory([FromServices] RecordPayCategoryUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestPayCategoryJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/pay-category/" + response.Id, response);
    }

    [HttpPost]
    [Route("pay-category/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponsePayCategoryJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> RevisePayCategory([FromServices] RecordPayCategoryUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestPayCategoryJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/pay-category/" + response.Id, response);
    }

    [HttpGet]
    [Route("pay-category/{id:guid}")]
    [ProducesResponseType(typeof(ResponsePayCategoryJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetPayCategory([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<EmployeePayCategory>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("pay-category")]
    [ProducesResponseType(typeof(List<ResponsePayCategoryJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListPayCategory([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<EmployeePayCategory>(ct)).Select(r => r.ToResponse()).ToList());

    [HttpPost]
    [Route("pay-item")]
    [ProducesResponseType(typeof(ResponsePayItemJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreatePayItem([FromServices] RecordPayItemUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestPayItemJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/pay-item/" + response.Id, response);
    }

    [HttpPost]
    [Route("pay-item/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponsePayItemJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> RevisePayItem([FromServices] RecordPayItemUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestPayItemJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/pay-item/" + response.Id, response);
    }

    [HttpGet]
    [Route("pay-item/{id:guid}")]
    [ProducesResponseType(typeof(ResponsePayItemJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetPayItem([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<EmployeePayItem>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("pay-item")]
    [ProducesResponseType(typeof(List<ResponsePayItemJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListPayItem([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<EmployeePayItem>(ct)).Select(r => r.ToResponse()).ToList());

    [HttpPost]
    [Route("commission-rule")]
    [ProducesResponseType(typeof(ResponseCommissionRuleJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreateCommissionRule([FromServices] RecordCommissionRuleUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestCommissionRuleJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/commission-rule/" + response.Id, response);
    }

    [HttpPost]
    [Route("commission-rule/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponseCommissionRuleJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ReviseCommissionRule([FromServices] RecordCommissionRuleUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestCommissionRuleJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/commission-rule/" + response.Id, response);
    }

    [HttpGet]
    [Route("commission-rule/{id:guid}")]
    [ProducesResponseType(typeof(ResponseCommissionRuleJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetCommissionRule([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<EmployeeCommissionRule>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("commission-rule")]
    [ProducesResponseType(typeof(List<ResponseCommissionRuleJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListCommissionRule([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<EmployeeCommissionRule>(ct)).Select(r => r.ToResponse()).ToList());

    [HttpPost]
    [Route("commission-source")]
    [ProducesResponseType(typeof(ResponseCommissionSourceJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreateCommissionSource([FromServices] RecordCommissionSourceUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestCommissionSourceJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/commission-source/" + response.Id, response);
    }

    [HttpPost]
    [Route("commission-source/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponseCommissionSourceJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ReviseCommissionSource([FromServices] RecordCommissionSourceUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestCommissionSourceJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/commission-source/" + response.Id, response);
    }

    [HttpGet]
    [Route("commission-source/{id:guid}")]
    [ProducesResponseType(typeof(ResponseCommissionSourceJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetCommissionSource([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<EmployeeCommissionSource>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("commission-source")]
    [ProducesResponseType(typeof(List<ResponseCommissionSourceJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListCommissionSource([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<EmployeeCommissionSource>(ct)).Select(r => r.ToResponse()).ToList());

    [HttpPost]
    [Route("hour-bank-entry")]
    [ProducesResponseType(typeof(ResponseHourBankEntryJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreateHourBankEntry([FromServices] RecordHourBankEntryUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestHourBankEntryJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/hour-bank-entry/" + response.Id, response);
    }

    [HttpPost]
    [Route("hour-bank-entry/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponseHourBankEntryJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ReviseHourBankEntry([FromServices] RecordHourBankEntryUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestHourBankEntryJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/hour-bank-entry/" + response.Id, response);
    }

    [HttpGet]
    [Route("hour-bank-entry/{id:guid}")]
    [ProducesResponseType(typeof(ResponseHourBankEntryJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetHourBankEntry([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<HourBankEntry>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("hour-bank-entry")]
    [ProducesResponseType(typeof(List<ResponseHourBankEntryJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListHourBankEntry([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<HourBankEntry>(ct)).Select(r => r.ToResponse()).ToList());

    [HttpPost]
    [Route("special-draw")]
    [ProducesResponseType(typeof(ResponseSpecialDrawJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreateSpecialDraw([FromServices] RecordSpecialDrawUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestSpecialDrawJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/special-draw/" + response.Id, response);
    }

    [HttpPost]
    [Route("special-draw/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponseSpecialDrawJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ReviseSpecialDraw([FromServices] RecordSpecialDrawUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestSpecialDrawJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/special-draw/" + response.Id, response);
    }

    [HttpGet]
    [Route("special-draw/{id:guid}")]
    [ProducesResponseType(typeof(ResponseSpecialDrawJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetSpecialDraw([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<SpecialDraw>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("special-draw")]
    [ProducesResponseType(typeof(List<ResponseSpecialDrawJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListSpecialDraw([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<SpecialDraw>(ct)).Select(r => r.ToResponse()).ToList());

    [HttpPost]
    [Route("special-draw-allocation")]
    [ProducesResponseType(typeof(ResponseSpecialDrawAllocationJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> CreateSpecialDrawAllocation([FromServices] RecordSpecialDrawAllocationUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key, [FromBody] RequestSpecialDrawAllocationJson request, CancellationToken ct)
    {
        if (request.PreviousId != null) throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);
        var response = await useCase.Execute(request, key, null, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/special-draw-allocation/" + response.Id, response);
    }

    [HttpPost]
    [Route("special-draw-allocation/{id:guid}/revision")]
    [ProducesResponseType(typeof(ResponseSpecialDrawAllocationJson), 201)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ReviseSpecialDrawAllocation([FromServices] RecordSpecialDrawAllocationUseCase useCase,
        [FromRoute] Guid id, [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? key,
        [FromHeader(Name = EntityTagHeader.IfMatchName)] string? ifMatch, [FromBody] RequestSpecialDrawAllocationJson request, CancellationToken ct)
    {
        var version = EntityTagHeader.ParseRequired(ifMatch);
        if (request.PreviousId != id) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var response = await useCase.Execute(request, key, version, ct);
        EntityTagHeader.Set(Response, response.Version);
        return Created("/people/special-draw-allocation/" + response.Id, response);
    }

    [HttpGet]
    [Route("special-draw-allocation/{id:guid}")]
    [ProducesResponseType(typeof(ResponseSpecialDrawAllocationJson), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> GetSpecialDrawAllocation([FromServices] PeopleRecordReader reader, [FromRoute] Guid id, CancellationToken ct)
    {
        var row = (await reader.List<SpecialDrawAllocation>(ct)).SingleOrDefault(r => r.Id == id)
            ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        EntityTagHeader.Set(Response, row.Version);
        return Ok(row.ToResponse());
    }

    [HttpGet]
    [Route("special-draw-allocation")]
    [ProducesResponseType(typeof(List<ResponseSpecialDrawAllocationJson>), 200)]
    [ProducesResponseType(typeof(ResponseErrorJson), 400)]
    [ProducesResponseType(typeof(ResponseErrorJson), 401)]
    [ProducesResponseType(typeof(ResponseErrorJson), 403)]
    [ProducesResponseType(typeof(ResponseErrorJson), 404)]
    [ProducesResponseType(typeof(ResponseErrorJson), 409)]
    public async Task<IActionResult> ListSpecialDrawAllocation([FromServices] PeopleRecordReader reader, CancellationToken ct) =>
        Ok((await reader.List<SpecialDrawAllocation>(ct)).Select(r => r.ToResponse()).ToList());

}
