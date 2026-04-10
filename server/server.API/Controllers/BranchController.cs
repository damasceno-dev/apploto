using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.BranchUsers.Add;
using server.Application.UseCases.BranchUsers.List;
using server.Application.UseCases.BranchUsers.Remove;
using server.Application.UseCases.BranchUsers.UpdateRole;
using server.Application.UseCases.Branches.Create;
using server.Application.UseCases.Branches.CreateSession;
using server.Application.UseCases.Branches.GetCurrentBranchSummary;
using server.Application.UseCases.Branches.ListMyBranches;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class BranchController : ControllerBase
{
    [HttpPost]
    [Route("create")]
    [TokenAuthenticate]
    [ProducesResponseType(typeof(ResponseCreateBranchJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromServices] CreateBranchUseCase createBranchUseCase,
        [FromBody] RequestCreateBranchJson request)
    {
        var response = await createBranchUseCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("my-branches")]
    [TokenAuthenticate]
    [ProducesResponseType(typeof(ResponseListMyBranchesJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMyBranches(
        [FromServices] ListMyBranchesUseCase listMyBranchesUseCase)
    {
        var response = await listMyBranchesUseCase.Execute();
        return Ok(response);
    }

    [HttpPost]
    [Route("session")]
    [TokenAuthenticate]
    [ProducesResponseType(typeof(ResponseCreateBranchSessionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSession(
        [FromServices] CreateBranchSessionUseCase createBranchSessionUseCase,
        [FromBody] RequestCreateBranchSessionJson request)
    {
        var response = await createBranchSessionUseCase.Execute(request);
        return Ok(response);
    }

    [HttpGet]
    [Route("current")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseGetCurrentBranchSummaryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(
        [FromServices] GetCurrentBranchSummaryUseCase getCurrentBranchSummaryUseCase)
    {
        var response = await getCurrentBranchSummaryUseCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("users")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseListBranchUsersJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(
        [FromServices] ListBranchUsersUseCase listBranchUsersUseCase)
    {
        var response = await listBranchUsersUseCase.Execute();
        return Ok(response);
    }

    [HttpPost]
    [Route("users")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseAddBranchUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUser(
        [FromServices] AddBranchUserUseCase addBranchUserUseCase,
        [FromBody] RequestAddBranchUserJson request)
    {
        var response = await addBranchUserUseCase.Execute(request);
        return Ok(response);
    }

    [HttpPut]
    [Route("users/{branchUserId:guid}/role")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseUpdateBranchUserRoleJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUserRole(
        [FromServices] UpdateBranchUserRoleUseCase updateBranchUserRoleUseCase,
        [FromRoute] Guid branchUserId,
        [FromBody] RequestUpdateBranchUserRoleJson request)
    {
        var response = await updateBranchUserRoleUseCase.Execute(branchUserId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("users/{branchUserId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseRemoveBranchUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveUser(
        [FromServices] RemoveBranchUserUseCase removeBranchUserUseCase,
        [FromRoute] Guid branchUserId)
    {
        var response = await removeBranchUserUseCase.Execute(branchUserId);
        return Ok(response);
    }
}
