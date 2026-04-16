using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.OperatorAccounts.AssignAccount;
using server.Application.UseCases.OperatorAccounts.GetOperatorSelfContext;
using server.Application.UseCases.OperatorAccounts.ListOperatorAccounts;
using server.Application.UseCases.OperatorAccounts.SetPrimaryAccount;
using server.Application.UseCases.OperatorAccounts.UnassignAccount;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("operator")]
[ApiController]
public class OperatorAccountController : ControllerBase
{
    [HttpGet]
    [Route("self-context")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseSelfContextJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSelfContext(
        [FromServices] GetOperatorSelfContextUseCase getOperatorSelfContextUseCase)
    {
        var response = await getOperatorSelfContextUseCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("{operatorId:guid}/accounts")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseListOperatorAccountsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromServices] ListOperatorAccountsUseCase listOperatorAccountsUseCase,
        [FromRoute] Guid operatorId)
    {
        var response = await listOperatorAccountsUseCase.Execute(operatorId);
        return Ok(response);
    }

    [HttpPost]
    [Route("{operatorId:guid}/accounts")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOperatorAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(
        [FromServices] AssignAccountUseCase assignAccountUseCase,
        [FromRoute] Guid operatorId,
        [FromBody] RequestAssignAccountJson request)
    {
        var response = await assignAccountUseCase.Execute(operatorId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{operatorId:guid}/accounts/{accountId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOperatorAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(
        [FromServices] UnassignAccountUseCase unassignAccountUseCase,
        [FromRoute] Guid operatorId,
        [FromRoute] Guid accountId)
    {
        var response = await unassignAccountUseCase.Execute(operatorId, accountId);
        return Ok(response);
    }

    [HttpPut]
    [Route("{operatorId:guid}/accounts/{accountId:guid}/primary")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOperatorAccountJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(
        [FromServices] SetPrimaryAccountUseCase setPrimaryAccountUseCase,
        [FromRoute] Guid operatorId,
        [FromRoute] Guid accountId)
    {
        var response = await setPrimaryAccountUseCase.Execute(operatorId, accountId);
        return Ok(response);
    }
}
