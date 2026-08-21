using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.Abstractions;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
