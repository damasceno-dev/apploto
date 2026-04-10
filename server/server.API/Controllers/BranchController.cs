using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
}
