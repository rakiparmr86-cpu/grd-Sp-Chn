using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Quality;
using GRD.SpChn.Warehouse.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Warehouse.Api.Controllers;

[ApiController]
[Route("purchase-orders/{purchaseOrderId:guid}/quality-inspection")]
public sealed class QualityInspectionsController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.QualityInspectionRead)]
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetQualityInspectionQuery(
            purchaseOrderId,
            User.GetRequiredOrganizationUnitId()), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
    }

    [Authorize(Policy = ErpPolicies.QualityInspectionPost)]
    [HttpPost]
    public async Task<IActionResult> Complete(
        Guid purchaseOrderId,
        [FromBody] CompleteQualityInspectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<QualityInspectionResult>(request.Result, true, out var inspectionResult))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Warehouse.InvalidQualityResult",
                detail: "Quality result must be Passed or Rejected.");
        }

        var result = await sender.Send(new CompleteQualityInspectionCommand(
            purchaseOrderId,
            User.GetRequiredOrganizationUnitId(),
            User.GetRequiredUserId(),
            inspectionResult,
            request.Notes), cancellationToken);
        return result.IsSuccess ? Created(string.Empty, result.Value) : ToProblem(result);
    }

    private IActionResult ToProblem<T>(Result<T> result) => Problem(
        statusCode: result.FirstError.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ when result.FirstError.Code == "Warehouse.WrongReceivingLocation" =>
                StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        },
        title: result.FirstError.Code,
        detail: result.FirstError.Description);
}

public sealed record CompleteQualityInspectionRequest(
    string Result,
    string? Notes);
