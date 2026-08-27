using GRD.SpChn.Organization.Domain;

namespace GRD.SpChn.Organization.Application.Abstractions;

public interface IOrganizationUnitRepository
{
    Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrganizationUnit>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(OrganizationUnit unit, CancellationToken cancellationToken = default);
}
