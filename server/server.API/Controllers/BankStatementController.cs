using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.BankStatements.AccountBinding;
using server.Application.UseCases.BankStatements.ListAccountBindings;
using server.Application.Services.Idempotency;
using server.Application.UseCases.BankStatements.Import;
using server.Application.UseCases.BankStatements.Propose;
using server.Application.UseCases.BankStatements.Confirm;
using server.Application.UseCases.BankStatements.Get;
using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("bank-statement")]
[ApiController]
public class BankStatementController : ControllerBase
{
    [HttpPost]
    [Route("import")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankStatementJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Import(
        [FromServices] BankStatementImportUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestBankStatementImportJson request,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(request, idempotencyKey, cancellationToken));

    [HttpPost]
    [Route("propose")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankMatchProposalsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Propose(
        [FromServices] ProposeBankMatchesUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestBankMatchProposeJson request,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(request, idempotencyKey, cancellationToken));

    [HttpPost]
    [Route("confirm")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankMatchConfirmationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        [FromServices] ConfirmBankMatchesUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestBankMatchConfirmJson request,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(request, idempotencyKey, cancellationToken));

    [HttpGet]
    [Route("{id:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankStatementJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReadImport(
        [FromServices] GetBankStatementUseCase useCase,
        [FromRoute] Guid id,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(id, cancellationToken));
    [HttpPost]
    [Route("account-binding")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseBankStatementAccountBindingJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAccountBinding(
        [FromServices] CreateBankStatementAccountBindingUseCase useCase,
        [FromHeader(Name = FinancialCommandIdempotency.HeaderName)] string? idempotencyKey,
        [FromBody] RequestBankStatementAccountBindingJson request,
        CancellationToken cancellationToken) => Created(string.Empty, await useCase.Execute(request, idempotencyKey, cancellationToken));

    [HttpGet]
    [Route("account-binding")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<ResponseBankStatementAccountBindingJson>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAccountBindings(
        [FromServices] ListBankStatementAccountBindingsUseCase useCase,
        [FromQuery] RequestListBankStatementAccountBindingsJson request,
        CancellationToken cancellationToken) => Ok(await useCase.Execute(request, cancellationToken));
}
