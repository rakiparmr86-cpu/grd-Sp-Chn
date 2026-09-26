using GRD.SpChn.Accounting.Application.Ledger;
using GRD.SpChn.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Accounting.Api.Controllers;

[ApiController]
[Route("ledger")]
public sealed class LedgerController(ISender sender) : ControllerBase
{
    private static readonly TimeSpan MaximumRange = TimeSpan.FromDays(366);

    [Authorize(Policy = ErpPolicies.AccountingJournalRead)]
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (!IsValidRange(fromUtc, toUtc)) return InvalidRange();

        return Ok(await sender.Send(
            new GetAccountLedgerQuery(fromUtc.ToUniversalTime(), toUtc.ToUniversalTime()),
            cancellationToken));
    }

    // Opening, period and closing totals per account for Trial Balance, P&L and Balance Sheet.
    [Authorize(Policy = ErpPolicies.AccountingJournalRead)]
    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (!IsValidRange(fromUtc, toUtc)) return InvalidRange();

        return Ok(await sender.Send(
            new GetAccountBalancesQuery(fromUtc.ToUniversalTime(), toUtc.ToUniversalTime()),
            cancellationToken));
    }

    private static bool IsValidRange(DateTime fromUtc, DateTime toUtc) =>
        toUtc > fromUtc && toUtc - fromUtc <= MaximumRange;

    private IActionResult InvalidRange() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Accounting.InvalidLedgerRange",
        detail: "Choose a To date after the From date, covering at most one year.");
}
