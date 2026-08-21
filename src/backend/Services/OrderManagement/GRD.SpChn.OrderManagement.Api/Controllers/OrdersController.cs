using GRD.SpChn.OrderManagement.Application.Orders;
using GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;
using GRD.SpChn.OrderManagement.Application.Orders.GetOrder;
using GRD.SpChn.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.OrderManagement.Api.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.CustomerId,
            (request.Items ?? [])
                .Select(item => new CreateOrderItem(item.ProductId, item.Quantity))
                .ToArray());
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return ToProblem(result);
        }

        var order = result.Value;
        return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOrderQuery(id), cancellationToken);
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

public sealed record CreateOrderRequest(
    Guid CustomerId,
    IReadOnlyCollection<CreateOrderItemRequest>? Items);

public sealed record CreateOrderItemRequest(Guid ProductId, decimal Quantity);
