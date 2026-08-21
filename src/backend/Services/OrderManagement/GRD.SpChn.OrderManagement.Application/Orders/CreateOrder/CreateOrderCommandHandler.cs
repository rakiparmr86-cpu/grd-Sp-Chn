using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    IOrderRepository repository,
    IOutboxWriter outboxWriter)
    : IRequestHandler<CreateOrderCommand, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(
            request.CustomerId,
            request.Items.Select(item =>
                OrderItem.Create(item.ProductId, item.Quantity)));
        var created = order.CreatedEvent
            ?? throw new InvalidOperationException("The order creation event was not raised.");
        var integrationEvent = new OrderPlacedIntegrationEvent(
            created.OrderId,
            created.OrderNumber,
            created.CustomerId,
            created.Items
                .Select(item => new OrderPlacedItem(item.ProductId, item.Quantity))
                .ToArray())
        {
            OccurredOnUtc = created.OccurredOnUtc
        };

        await repository.AddAsync(order, cancellationToken);
        await outboxWriter.AddAsync(
            integrationEvent,
            MessagingTopology.OrderExchange,
            MessagingTopology.OrderPlacedRoutingKey,
            cancellationToken);

        return Result<OrderResponse>.Success(OrderResponse.From(order));
    }
}
