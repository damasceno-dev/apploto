using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Reports.DailyLedger;
using server.Application.UseCases.Reports.FiadoBalance;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.Controllers;

[Route("[controller]")]
[ApiController]
public class ReportController : ControllerBase
{
    [HttpGet]
    [Route("daily-ledger")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseDailyLedgerJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DailyLedger(
        [FromServices] GetDailyLedgerUseCase useCase,
        [FromQuery] RequestDailyLedgerJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpGet]
    [Route("fiado/balance")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseFiadoBalanceJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FiadoBalance(
        [FromServices] GetFiadoBalancesUseCase useCase,
        [FromQuery] RequestFiadoBalanceJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }
}
