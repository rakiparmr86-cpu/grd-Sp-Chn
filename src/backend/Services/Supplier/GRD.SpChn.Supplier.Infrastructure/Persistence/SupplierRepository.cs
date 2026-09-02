using Dapper;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Supplier.Application.Abstractions;
using GRD.SpChn.Supplier.Domain;

namespace GRD.SpChn.Supplier.Infrastructure.Persistence;

internal sealed class SupplierRepository(IDbConnectionFactory connectionFactory)
    : ISupplierRepository
{
    public async Task<IReadOnlyCollection<SupplierProfile>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SupplierRow>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, legal_name AS LegalName,
                   display_name AS DisplayName,
                   tax_identification_number AS TaxIdentificationNumber,
                   email AS Email, phone AS Phone, address_line_1 AS AddressLine1,
                   city AS City, state_name AS State, postal_code AS PostalCode,
                   country_code AS CountryCode,
                   payment_terms_days AS PaymentTermsDays,
                   default_currency AS DefaultCurrency, status AS Status,
                   is_active AS IsActive, created_on_utc AS CreatedOnUtc,
                   updated_on_utc AS UpdatedOnUtc
            FROM supplier_master
            WHERE is_active = TRUE AND status = 'Active'
            ORDER BY display_name, code;
            """,
            cancellationToken: cancellationToken));

        return rows.Select(Map).ToArray();
    }

    private static SupplierProfile Map(SupplierRow row) => SupplierProfile.Rehydrate(
        row.Id, row.Code, row.LegalName, row.DisplayName,
        row.TaxIdentificationNumber, row.Email, row.Phone, row.AddressLine1,
        row.City, row.State, row.PostalCode, row.CountryCode,
        row.PaymentTermsDays, row.DefaultCurrency, row.Status, row.IsActive,
        DateTime.SpecifyKind(row.CreatedOnUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(row.UpdatedOnUtc, DateTimeKind.Utc));

    private sealed record SupplierRow(
        Guid Id, string Code, string LegalName, string DisplayName,
        string? TaxIdentificationNumber, string? Email, string? Phone,
        string? AddressLine1, string? City, string? State, string? PostalCode,
        string CountryCode, int PaymentTermsDays, string DefaultCurrency,
        string Status, bool IsActive, DateTime CreatedOnUtc, DateTime UpdatedOnUtc);
}
