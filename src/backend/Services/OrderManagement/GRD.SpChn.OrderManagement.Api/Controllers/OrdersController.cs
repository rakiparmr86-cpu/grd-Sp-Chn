using FluentValidation;
using GRD.SpChn.OrderManagement.Application.Orders;
using GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;
using GRD.SpChn.OrderManagement.Application.Orders.GetOrder;
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
        [FromServices] IValidator<CreateOrderCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.CustomerId,
            (request.Items ?? [])
                .Select(item => new CreateOrderItem(item.ProductId, item.Quantity))
                .ToArray());
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(
                new ValidationProblemDetails(validation.ToDictionary()));
        }

        var order = await sender.Send(command, cancellationToken);
        return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await sender.Send(new GetOrderQuery(id), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}

public sealed record CreateOrderRequest(
    Guid CustomerId,
    IReadOnlyCollection<CreateOrderItemRequest>? Items);

public sealed record CreateOrderItemRequest(Guid ProductId, decimal Quantity);
