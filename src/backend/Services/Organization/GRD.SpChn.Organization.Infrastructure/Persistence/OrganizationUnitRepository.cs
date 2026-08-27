using Dapper;
using GRD.SpChn.Organization.Application.Abstractions;
using GRD.SpChn.Organization.Domain;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Organization.Infrastructure.Persistence;

internal sealed class OrganizationUnitRepository(IDbConnectionFactory connectionFactory)
    : IOrganizationUnitRepository
{
    public async Task<OrganizationUnit?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UnitRow>(new CommandDefinition(
            SelectSql + " WHERE id = @Id;",
            new { Id = id },
            cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyCollection<OrganizationUnit>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UnitRow>(new CommandDefinition(
            SelectSql + " ORDER BY created_on_utc, code;",
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM organization_units WHERE code = UPPER(@Code));",
            new { Code = code },
            cancellationToken: cancellationToken));
    }

    public async Task AddAsync(
        OrganizationUnit unit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO organization_units
                (id, parent_id, code, name, unit_type, is_active, created_on_utc)
            VALUES
                (@Id, @ParentId, @Code, @Name, @Type, @IsActive, @CreatedOnUtc);
            """,
            new
            {
                unit.Id,
                unit.ParentId,
                unit.Code,
                unit.Name,
                Type = unit.Type.ToString(),
                unit.IsActive,
                unit.CreatedOnUtc
            },
            cancellationToken: cancellationToken));
    }

    private const string SelectSql = """
        SELECT id AS Id,
               parent_id AS ParentId,
               code AS Code,
               name AS Name,
               unit_type AS Type,
               is_active AS IsActive,
               created_on_utc AS CreatedOnUtc
        FROM organization_units
        """;

    private static OrganizationUnit Map(UnitRow row) => OrganizationUnit.Rehydrate(
        row.Id,
        row.ParentId,
        row.Code,
        row.Name,
        Enum.Parse<OrganizationUnitType>(row.Type, true),
        row.IsActive,
        DateTime.SpecifyKind(row.CreatedOnUtc, DateTimeKind.Utc));

    private sealed record UnitRow(
        Guid Id,
        Guid? ParentId,
        string Code,
        string Name,
        string Type,
        bool IsActive,
        DateTime CreatedOnUtc);
}
