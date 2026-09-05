namespace GRD.SpChn.ProductCatalog.Domain;

public sealed record CatalogItem(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string CategoryCode,
    string CategoryName,
    string BaseUnitOfMeasure,
    string UnitOfMeasureName,
    bool ProcurementAllowed,
    bool InventoryTracked,
    bool IsActive);
