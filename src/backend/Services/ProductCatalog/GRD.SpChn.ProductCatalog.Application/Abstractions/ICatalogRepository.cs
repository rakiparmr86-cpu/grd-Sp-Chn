using GRD.SpChn.ProductCatalog.Domain;

namespace GRD.SpChn.ProductCatalog.Application.Abstractions;

public interface ICatalogRepository
{
    Task<IReadOnlyCollection<CatalogItem>> GetActiveProcurementItemsAsync(
        CancellationToken cancellationToken = default);
}
