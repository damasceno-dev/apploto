using Microsoft.AspNetCore.Mvc;
using server.Application.UseCases.Reports.CashVariance;
using server.Application.UseCases.Reports.DailyLedger;
using server.Application.UseCases.Reports.FiadoAging;
using server.Application.UseCases.Reports.FiadoBalance;
using server.Application.UseCases.Reports.MonthlyReconciliation;
using server.Application.UseCases.Reports.OpenChequeAging;
using server.Application.UseCases.Reports.OperatorSummary;
using server.Application.UseCases.Reports.TimeEntryBalance;
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

    [HttpGet]
    [Route("fiado/aging")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseFiadoAgingJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FiadoAging(
        [FromServices] GetFiadoAgingUseCase useCase,
        [FromQuery] RequestFiadoAgingJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpGet]
    [Route("cheques/open-aging")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseOpenChequeAgingJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenChequeAging(
        [FromServices] GetOpenChequeAgingUseCase useCase,
        [FromQuery] RequestOpenChequeAgingJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpGet]
    [Route("cash-variance")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseCashVarianceSummaryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CashVarianceSummary(
        [FromServices] GetCashVarianceSummaryUseCase useCase,
        [FromQuery] RequestCashVarianceSummaryJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpGet]
    [Route("monthly-reconciliation/{year:int:min(2000):max(2100)}/{month:int:min(1):max(12)}")]
    [TokenAuthorize(Role.Manager, Role.Admin)]
    [ProducesResponseType(typeof(ResponseMonthlyReconciliationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MonthlyReconciliation(
        [FromServices] GetMonthlyReconciliationUseCase useCase,
        [FromRoute] int year,
        [FromRoute] int month)
    {
        var response = await useCase.Execute(new RequestMonthlyReconciliationJson
        {
            Year = year,
            Month = month
        });
        return Ok(response);
    }

    [HttpGet]
    [Route("operator-summary")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseOperatorTransactionSummaryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OperatorSummary(
        [FromServices] GetOperatorTransactionSummaryUseCase useCase,
        [FromQuery] RequestOperatorTransactionSummaryJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }

    [HttpGet]
    [Route("timeentry-balance")]
    [TokenAuthenticateBranch]
    [ProducesResponseType(typeof(ResponseTimeEntryBalanceSummaryJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TimeEntryBalance(
        [FromServices] GetTimeEntryBalanceSummaryUseCase useCase,
        [FromQuery] RequestTimeEntryBalanceSummaryJson request)
    {
        var response = await useCase.Execute(request);
        return Ok(response);
    }
}
