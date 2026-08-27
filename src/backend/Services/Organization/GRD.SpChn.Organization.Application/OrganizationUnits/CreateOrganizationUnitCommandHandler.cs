using GRD.SpChn.Organization.Application.Abstractions;
using GRD.SpChn.Organization.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Organization.Application.OrganizationUnits;

internal sealed class CreateOrganizationUnitCommandHandler(IOrganizationUnitRepository repository)
    : IRequestHandler<CreateOrganizationUnitCommand, Result<OrganizationUnitResponse>>
{
    public async Task<Result<OrganizationUnitResponse>> Handle(
        CreateOrganizationUnitCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrganizationUnitType>(request.Type, true, out var type))
        {
            return Result<OrganizationUnitResponse>.Failure(Error.Validation(
                "Organization.InvalidType",
                $"'{request.Type}' is not a supported organization unit type.",
                nameof(request.Type)));
        }

        var parent = request.ParentId is null
            ? null
            : await repository.GetByIdAsync(request.ParentId.Value, cancellationToken);
        if (request.ParentId is not null && parent is null)
        {
            return Result<OrganizationUnitResponse>.Failure(Error.NotFound(
                "Organization.ParentNotFound",
                $"Parent organization unit '{request.ParentId}' was not found."));
        }

        if (await repository.CodeExistsAsync(request.Code.Trim(), cancellationToken))
        {
            return Result<OrganizationUnitResponse>.Failure(Error.Conflict(
                "Organization.CodeExists",
                $"Organization unit code '{request.Code}' already exists."));
        }

        try
        {
            var unit = OrganizationUnit.Create(
                request.ParentId,
                request.Code,
                request.Name,
                type,
                parent?.Type);
            await repository.AddAsync(unit, cancellationToken);
            return Result<OrganizationUnitResponse>.Success(OrganizationUnitResponse.From(unit));
        }
        catch (ArgumentException exception)
        {
            return Result<OrganizationUnitResponse>.Failure(Error.Validation(
                "Organization.InvalidHierarchy",
                exception.Message));
        }
    }
}
