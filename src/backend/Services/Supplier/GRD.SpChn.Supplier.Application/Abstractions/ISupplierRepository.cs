using GRD.SpChn.Supplier.Domain;

namespace GRD.SpChn.Supplier.Application.Abstractions;

public interface ISupplierRepository
{
    Task<IReadOnlyCollection<SupplierProfile>> GetActiveAsync(
        CancellationToken cancellationToken = default);
}
