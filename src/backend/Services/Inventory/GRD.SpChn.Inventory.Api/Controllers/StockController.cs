using FluentValidation;
using GRD.SpChn.Inventory.Application.Stock;
using GRD.SpChn.Inventory.Application.Stock.GetStock;
using GRD.SpChn.Inventory.Application.Stock.SetStock;
using MediatR;
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
        [FromServices] IValidator<SetStockCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SetStockCommand(productId, request.AvailableQuantity);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(
                new ValidationProblemDetails(validation.ToDictionary()));
        }

        return Ok(await sender.Send(command, cancellationToken));
    }

    [HttpGet("{productId:guid}")]
    [ProducesResponseType<StockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProductId(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var stock = await sender.Send(new GetStockQuery(productId), cancellationToken);
        return stock is null ? NotFound() : Ok(stock);
    }
}

public sealed record SetStockRequest(decimal AvailableQuantity);
