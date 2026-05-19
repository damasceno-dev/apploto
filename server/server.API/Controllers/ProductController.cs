using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Products.Create;
using server.Application.UseCases.Products.Deactivate;
using server.Application.UseCases.Products.Get;
using server.Application.UseCases.Products.List;
using server.Application.UseCases.Products.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("product")]
[ApiController]
public class ProductController : ControllerBase
{
    [HttpPost]
    [Route("")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseProductJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromServices] CreateProductUseCase useCase,
        [FromBody] RequestCreateProductJson request)
    {
        var response = await useCase.Execute(request);
        return Created(string.Empty, response);
    }

    [HttpGet]
    [Route("")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseListProductsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromServices] ListProductsUseCase useCase)
    {
        var response = await useCase.Execute();
        return Ok(response);
    }

    [HttpGet]
    [Route("{productId:guid}")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseProductJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromServices] GetProductUseCase useCase,
        [FromRoute] Guid productId)
    {
        var response = await useCase.Execute(productId);
        return Ok(response);
    }

    [HttpPut]
    [Route("{productId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseProductJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromServices] UpdateProductUseCase useCase,
        [FromRoute] Guid productId,
        [FromBody] RequestUpdateProductJson request)
    {
        var response = await useCase.Execute(productId, request);
        return Ok(response);
    }

    [HttpDelete]
    [Route("{productId:guid}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(
        [FromServices] DeactivateProductUseCase useCase,
        [FromRoute] Guid productId)
    {
        await useCase.Execute(productId);
        return NoContent();
    }
}
