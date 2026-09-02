namespace GRD.SpChn.Supplier.Domain;

public sealed class SupplierProfile
{
    private SupplierProfile(
        Guid id, string code, string legalName, string displayName,
        string? taxIdentificationNumber, string? email, string? phone,
        string? addressLine1, string? city, string? state, string? postalCode,
        string countryCode, int paymentTermsDays, string defaultCurrency,
        string status, bool isActive, DateTime createdOnUtc, DateTime updatedOnUtc)
    {
        Id = id;
        Code = code;
        LegalName = legalName;
        DisplayName = displayName;
        TaxIdentificationNumber = taxIdentificationNumber;
        Email = email;
        Phone = phone;
        AddressLine1 = addressLine1;
        City = city;
        State = state;
        PostalCode = postalCode;
        CountryCode = countryCode;
        PaymentTermsDays = paymentTermsDays;
        DefaultCurrency = defaultCurrency;
        Status = status;
        IsActive = isActive;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }

    public Guid Id { get; }
    public string Code { get; }
    public string LegalName { get; }
    public string DisplayName { get; }
    public string? TaxIdentificationNumber { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? AddressLine1 { get; }
    public string? City { get; }
    public string? State { get; }
    public string? PostalCode { get; }
    public string CountryCode { get; }
    public int PaymentTermsDays { get; }
    public string DefaultCurrency { get; }
    public string Status { get; }
    public bool IsActive { get; }
    public DateTime CreatedOnUtc { get; }
    public DateTime UpdatedOnUtc { get; }

    public static SupplierProfile Rehydrate(
        Guid id, string code, string legalName, string displayName,
        string? taxIdentificationNumber, string? email, string? phone,
        string? addressLine1, string? city, string? state, string? postalCode,
        string countryCode, int paymentTermsDays, string defaultCurrency,
        string status, bool isActive, DateTime createdOnUtc, DateTime updatedOnUtc) =>
        new(id, code, legalName, displayName, taxIdentificationNumber, email,
            phone, addressLine1, city, state, postalCode, countryCode,
            paymentTermsDays, defaultCurrency, status, isActive,
            createdOnUtc, updatedOnUtc);
}
