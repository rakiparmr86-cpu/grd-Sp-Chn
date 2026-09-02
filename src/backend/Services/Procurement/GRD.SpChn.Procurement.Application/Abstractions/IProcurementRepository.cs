using GRD.SpChn.Procurement.Domain;
using GRD.SpChn.Procurement.Application.MaterialRequests;

namespace GRD.SpChn.Procurement.Application.Abstractions;

public interface IProcurementRepository
{
    Task AddMaterialRequestAsync(MaterialRequest request, CancellationToken cancellationToken = default);
    Task<MaterialRequest?> GetMaterialRequestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MaterialRequestListItemResponse>> ListMaterialRequestsAsync(
        Guid organizationUnitId,
        bool includeAllOrganizationUnits,
        CancellationToken cancellationToken = default);
    Task<MaterialRequest?> GetMaterialRequestForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateMaterialRequestAsync(MaterialRequest request, CancellationToken cancellationToken = default);
    Task AddPurchaseOrderAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PurchaseOrder>> ListPurchaseOrdersAsync(
        Guid organizationUnitId,
        bool includeAllOrganizationUnits,
        CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetPurchaseOrderForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);
}
