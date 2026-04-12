using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Operators.Create;
using server.Application.UseCases.Operators.Deactivate;
using server.Application.UseCases.Operators.Get;
using server.Application.UseCases.Operators.List;
using server.Application.UseCases.Operators.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class OperatorController : ControllerBase
{
    [HttpPost]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCreateOperatorJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromServices] CreateOperatorUseCase createOperatorUseCase,
        [FromBody] RequestCreateOperatorJson request)
    {
        var response = await createOperatorUseCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseListOperatorsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromServices] ListOperatorsUseCase listOperatorsUseCase)
    {
        var response = await listOperatorsUseCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("{operatorId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOperatorJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetOperatorUseCase getOperatorUseCase,
        [FromRoute] Guid operatorId)
    {
        var response = await getOperatorUseCase.Execute(operatorId);
        return Ok(response);
    }

    [HttpPut]
    [Route("{operatorId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOperatorJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateOperatorUseCase updateOperatorUseCase,
        [FromRoute] Guid operatorId,
        [FromBody] RequestUpdateOperatorJson request)
    {
        var response = await updateOperatorUseCase.Execute(operatorId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{operatorId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOperatorJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateOperatorUseCase deactivateOperatorUseCase,
        [FromRoute] Guid operatorId)
    {
        var response = await deactivateOperatorUseCase.Execute(operatorId);
        return Ok(response);
    }
}
