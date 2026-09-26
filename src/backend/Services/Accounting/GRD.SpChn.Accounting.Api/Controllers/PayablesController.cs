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
    [Authorize(Policy = ErpPolicies.AccountingInvoiceCreate)]
    [HttpGet("invoice-candidates")]
    public async Task<IActionResult> ListInvoiceCandidates(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListInvoiceCandidatesQuery(), cancellationToken));

    [Authorize(Policy = ErpPolicies.AccountingPayableRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListPayablesQuery(), cancellationToken));

    [Authorize(Policy = ErpPolicies.AccountingPayableRead)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var detail = await sender.Send(new GetPayableDetailQuery(id), cancellationToken);
        return detail is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Accounting.PayableNotFound",
                detail: $"Payable '{id}' was not found.")
            : Ok(detail);
    }

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
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApproveVendorPayablesCommand([id], User.GetRequiredUserId()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value.Single()) : ToProblem(result.FirstError);
    }

    [Authorize(Policy = ErpPolicies.AccountingPayableApprove)]
    [HttpPost("batch-approvals")]
    public async Task<IActionResult> ApproveBatch(
        [FromBody] PayableBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApproveVendorPayablesCommand(request.PayableIds ?? [], User.GetRequiredUserId()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.FirstError);
    }

    [Authorize(Policy = ErpPolicies.AccountingPaymentRelease)]
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordVendorPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RecordVendorPaymentBatchCommand(
            [id],
            User.GetRequiredUserId(),
            request.BankReference,
            request.PaidOnUtc ?? DateTime.UtcNow), cancellationToken);
        return result.IsSuccess ? Ok(result.Value.Payables.Single()) : ToProblem(result.FirstError);
    }

    [Authorize(Policy = ErpPolicies.AccountingPaymentRelease)]
    [HttpPost("payment-batches")]
    public async Task<IActionResult> RecordPaymentBatch(
        [FromBody] RecordVendorPaymentBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RecordVendorPaymentBatchCommand(
            request.PayableIds ?? [],
            User.GetRequiredUserId(),
            request.BankReference,
            request.PaidOnUtc ?? DateTime.UtcNow), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ToProblem(result.FirstError);
    }

    private ObjectResult ToActionResult(Result<PayableResponse> result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess ? StatusCode(successStatus, result.Value) : ToProblem(result.FirstError);

    private ObjectResult ToProblem(Error error) => Problem(
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        },
        title: error.Code,
        detail: error.Description);
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
public sealed record PayableBatchRequest(IReadOnlyCollection<Guid>? PayableIds);
public sealed record RecordVendorPaymentBatchRequest(
    IReadOnlyCollection<Guid>? PayableIds,
    string BankReference,
    DateTime? PaidOnUtc);
