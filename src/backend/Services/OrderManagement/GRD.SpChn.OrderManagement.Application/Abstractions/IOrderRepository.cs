using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.Abstractions;

public interface IOrderRepository
{
    Task AddAsync(
        Order order,
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<bool> ApplyReservationResultAsync(
        Guid eventId,
        string eventType,
        Guid orderId,
        OrderStatus status,
        CancellationToken cancellationToken = default);
}
