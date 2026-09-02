using GRD.SpChn.Supplier.Application.Abstractions;
using GRD.SpChn.Supplier.Domain;
using MediatR;

namespace GRD.SpChn.Supplier.Application.Suppliers;

public sealed record GetActiveSuppliersQuery
    : IRequest<IReadOnlyCollection<SupplierResponse>>;

public sealed record SupplierResponse(
    Guid Id, string Code, string LegalName, string DisplayName,
    string? TaxIdentificationNumber, string? Email, string? Phone,
    string? AddressLine1, string? City, string? State, string? PostalCode,
    string CountryCode, int PaymentTermsDays, string DefaultCurrency,
    string Status)
{
    public static SupplierResponse From(SupplierProfile supplier) =>
        new(supplier.Id, supplier.Code, supplier.LegalName, supplier.DisplayName,
            supplier.TaxIdentificationNumber, supplier.Email, supplier.Phone,
            supplier.AddressLine1, supplier.City, supplier.State, supplier.PostalCode,
            supplier.CountryCode, supplier.PaymentTermsDays,
            supplier.DefaultCurrency, supplier.Status);
}

internal sealed class GetActiveSuppliersQueryHandler(ISupplierRepository repository)
    : IRequestHandler<GetActiveSuppliersQuery, IReadOnlyCollection<SupplierResponse>>
{
    public async Task<IReadOnlyCollection<SupplierResponse>> Handle(
        GetActiveSuppliersQuery request,
        CancellationToken cancellationToken) =>
        (await repository.GetActiveAsync(cancellationToken))
            .Select(SupplierResponse.From)
            .ToArray();
}
