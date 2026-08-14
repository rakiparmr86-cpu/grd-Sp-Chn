using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Inventory.Domain;

namespace GRD.SpChn.Inventory.Application.Abstractions;

public interface IInventoryRepository
{
    Task<ReservationProcessingResult> ReserveForOrderAsync(
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);

    Task<StockItem> SetAvailableQuantityAsync(
        Guid productId,
        decimal availableQuantity,
        CancellationToken cancellationToken = default);

    Task<StockItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}

public sealed record ReservationProcessingResult(
    bool IsDuplicate,
    bool Reserved,
    string? FailureReason);
