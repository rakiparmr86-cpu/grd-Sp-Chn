using Dapper;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.ProductCatalog.Application.Abstractions;
using GRD.SpChn.ProductCatalog.Domain;

namespace GRD.SpChn.ProductCatalog.Infrastructure.Persistence;

internal sealed class CatalogRepository(IDbConnectionFactory connectionFactory)
    : ICatalogRepository
{
    public async Task<IReadOnlyCollection<CatalogItem>> GetActiveProcurementItemsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CatalogItemRow>(new CommandDefinition(
            """
            SELECT item.id AS Id, item.code AS Code, item.name AS Name,
                   item.description AS Description,
                   item.category_code AS CategoryCode,
                   category.name AS CategoryName,
                   item.base_uom_code AS BaseUnitOfMeasure,
                   uom.name AS UnitOfMeasureName,
                   item.procurement_allowed AS ProcurementAllowed,
                   item.inventory_tracked AS InventoryTracked,
                   item.is_active AS IsActive
            FROM catalog_items item
            INNER JOIN catalog_categories category
                    ON category.code = item.category_code
            INNER JOIN catalog_units_of_measure uom
                    ON uom.code = item.base_uom_code
            WHERE item.is_active = TRUE
              AND item.procurement_allowed = TRUE
              AND category.is_active = TRUE
              AND uom.is_active = TRUE
            ORDER BY category.name, item.name, item.code;
            """,
            cancellationToken: cancellationToken));

        return rows.Select(row => new CatalogItem(
            row.Id,
            row.Code,
            row.Name,
            row.Description,
            row.CategoryCode,
            row.CategoryName,
            row.BaseUnitOfMeasure,
            row.UnitOfMeasureName,
            row.ProcurementAllowed,
            row.InventoryTracked,
            row.IsActive)).ToArray();
    }

    private sealed record CatalogItemRow(
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
}
