using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Accounts.CreateBank;
using server.Application.UseCases.Accounts.CreateTab;
using server.Application.UseCases.Accounts.CreateTerminal;
using server.Application.UseCases.Accounts.Deactivate;
using server.Application.UseCases.Accounts.Get;
using server.Application.UseCases.Accounts.List;
using server.Application.UseCases.Accounts.PairTab;
using server.Application.UseCases.Accounts.UnpairTab;
using server.Application.UseCases.Accounts.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    [HttpPost]
    [Route("bank")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCreateAccountJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBank(
        [FromServices] CreateBankAccountUseCase createBankAccountUseCase,
        [FromBody] RequestCreateBankAccountJson request)
    {
        var response = await createBankAccountUseCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("terminal")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCreateAccountJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTerminal(
        [FromServices] CreateTerminalAccountUseCase createTerminalAccountUseCase,
        [FromBody] RequestCreateTerminalAccountJson request)
    {
        var response = await createTerminalAccountUseCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("tab")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCreateAccountJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTab(
        [FromServices] CreateTabAccountUseCase createTabAccountUseCase,
        [FromBody] RequestCreateTabAccountJson request)
    {
        var response = await createTabAccountUseCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpPost]
    [Route("pair-tab")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PairTab(
        [FromServices] PairTabAccountUseCase pairTabAccountUseCase,
        [FromBody] RequestPairTabAccountJson request)
    {
        var response = await pairTabAccountUseCase.Execute(request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("terminal/{terminalAccountId:guid}/tab")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpairTab(
        [FromServices] UnpairTabAccountUseCase unpairTabAccountUseCase,
        [FromRoute] Guid terminalAccountId)
    {
        var response = await unpairTabAccountUseCase.Execute(terminalAccountId);
        return Ok(response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseListAccountsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromServices] ListAccountsUseCase listAccountsUseCase)
    {
        var response = await listAccountsUseCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("{accountId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetAccountUseCase getAccountUseCase,
        [FromRoute] Guid accountId)
    {
        var response = await getAccountUseCase.Execute(accountId);
        return Ok(response);
    }

    [HttpPut]
    [Route("{accountId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateAccountUseCase updateAccountUseCase,
        [FromRoute] Guid accountId,
        [FromBody] RequestUpdateAccountJson request)
    {
        var response = await updateAccountUseCase.Execute(accountId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{accountId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateAccountUseCase deactivateAccountUseCase,
        [FromRoute] Guid accountId)
    {
        var response = await deactivateAccountUseCase.Execute(accountId);
        return Ok(response);
    }
}
