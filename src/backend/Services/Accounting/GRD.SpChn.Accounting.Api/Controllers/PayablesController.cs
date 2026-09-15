using GRD.SpChn.Accounting.Application.Payables;
using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Accounting.Api.Controllers;

[ApiController]
[Route("payables")]
public sealed class PayablesController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.AccountingPayableRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListPayablesQuery(), cancellationToken));

    [Authorize(Policy = ErpPolicies.AccountingInvoiceCreate)]
    [HttpPost("vendor-invoices")]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateVendorInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateVendorInvoiceCommand(
            request.PurchaseOrderId,
            request.GoodsReceiptId,
            request.SupplierInvoiceNumber,
            request.InvoiceDateUtc,
            request.DueDateUtc,
            request.TaxAmount,
            User.GetRequiredUserId(),
            request.Lines.Select(line => new InvoiceLineInput(
                line.ProductId,
                line.Quantity,
                line.UnitPrice)).ToArray()), cancellationToken);
        return ToActionResult(result, StatusCodes.Status201Created);
    }

    [Authorize(Policy = ErpPolicies.AccountingPayableApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await sender.Send(
            new ApproveVendorPayableCommand(id, User.GetRequiredUserId()),
            cancellationToken));

    [Authorize(Policy = ErpPolicies.AccountingPaymentRelease)]
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordVendorPaymentRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await sender.Send(new RecordVendorPaymentCommand(
            id,
            User.GetRequiredUserId(),
            request.BankReference,
            request.PaidOnUtc ?? DateTime.UtcNow), cancellationToken));

    private ObjectResult ToActionResult(Result<PayableResponse> result, int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
            return StatusCode(successStatus, result.Value);

        var status = result.FirstError.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        return Problem(
            statusCode: status,
            title: result.FirstError.Code,
            detail: result.FirstError.Description);
    }
}

public sealed record CreateVendorInvoiceRequest(
    Guid PurchaseOrderId,
    Guid GoodsReceiptId,
    string SupplierInvoiceNumber,
    DateTime InvoiceDateUtc,
    DateTime DueDateUtc,
    decimal TaxAmount,
    IReadOnlyCollection<CreateVendorInvoiceLineRequest> Lines);

public sealed record CreateVendorInvoiceLineRequest(Guid ProductId, decimal Quantity, decimal UnitPrice);
public sealed record RecordVendorPaymentRequest(string BankReference, DateTime? PaidOnUtc);
