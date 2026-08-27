using GRD.SpChn.Inventory.Application.Stock;
using GRD.SpChn.Inventory.Application.Stock.GetStock;
using GRD.SpChn.Inventory.Application.Stock.GetLocationStock;
using GRD.SpChn.Inventory.Application.Stock.SetStock;
using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Inventory.Api.Controllers;

[ApiController]
[Route("stock")]
public sealed class StockController(ISender sender) : ControllerBase
{
    [HttpPut("{productId:guid}")]
    [ProducesResponseType<StockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetAvailableQuantity(
        Guid productId,
        [FromBody] SetStockRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetStockCommand(productId, request.AvailableQuantity);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
    }

    [HttpGet("{productId:guid}")]
    [ProducesResponseType<StockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProductId(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStockQuery(productId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
    }

    [Authorize(Policy = ErpPolicies.InventoryStockRead)]
    [HttpGet("locations/{organizationUnitId:guid}/{productId:guid}")]
    public async Task<IActionResult> GetLocationStock(
        Guid organizationUnitId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetLocationStockQuery(organizationUnitId, productId),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
    }

    private IActionResult ToProblem<T>(Result<T> result)
    {
        if (result.Errors.All(error => error.Type == ErrorType.Validation))
        {
            var errors = result.Errors
                .GroupBy(error => error.Target ?? error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var error = result.FirstError;
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description);
    }
}

public sealed record SetStockRequest(decimal AvailableQuantity);
