using GRD.SpChn.Accounting.Application.Payables;
using GRD.SpChn.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Accounting.Api.Controllers;

[ApiController]
[Route("journal-entries")]
public sealed class JournalEntriesController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.AccountingJournalRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListJournalEntriesQuery(), cancellationToken));
}
