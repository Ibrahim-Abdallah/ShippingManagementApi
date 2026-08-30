using System.ComponentModel.DataAnnotations;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Carriers;
using ShippingManagementApi.Domain.Quotes;

namespace ShippingManagementApi.Application.Quotes;

public sealed record QuoteAddressRequest(string? CountryCode, string? City, string? StateOrProvince = null, string? PostalCode = null, string? AddressLine1 = null);
public sealed record QuotePackageRequest(decimal? Weight, WeightUnit? WeightUnit, decimal? Length = null, decimal? Width = null,
    decimal? Height = null, DimensionUnit? DimensionUnit = null, decimal? DeclaredValue = null);
public sealed record CreateShippingQuoteRequest(QuoteAddressRequest? Origin, QuoteAddressRequest? Destination,
    IReadOnlyCollection<QuotePackageRequest>? Packages, string? Currency, IReadOnlyCollection<ServiceLevel>? RequestedServiceLevels = null,
    bool RequiresCod = false) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Origin is null) yield return new("Origin is required.", [nameof(Origin)]);
        else foreach (var error in ValidateAddress(Origin, nameof(Origin))) yield return error;
        if (Destination is null) yield return new("Destination is required.", [nameof(Destination)]);
        else foreach (var error in ValidateAddress(Destination, nameof(Destination))) yield return error;
        if (string.IsNullOrWhiteSpace(Currency)) yield return new("Currency is required.", [nameof(Currency)]);
        if (Packages is null || Packages.Count == 0) yield return new("At least one package is required.", [nameof(Packages)]);
        else if (Packages.Count > ShippingQuote.MaximumPackages) yield return new($"At most {ShippingQuote.MaximumPackages} packages are allowed.", [nameof(Packages)]);
        if (Packages is not null) for (var i = 0; i < Packages.Count; i++)
        {
            var p = Packages.ElementAt(i); var prefix = $"Packages[{i}]";
            if (p.Weight is null) yield return new("Weight is required.", [$"{prefix}.Weight"]);
            if (p.WeightUnit is null) yield return new("WeightUnit is required.", [$"{prefix}.WeightUnit"]);
            var dimensions = new[] { p.Length.HasValue, p.Width.HasValue, p.Height.HasValue, p.DimensionUnit.HasValue };
            if (dimensions.Any(x => x) && !dimensions.All(x => x)) yield return new("Length, width, height, and dimension unit must be supplied together.", [prefix]);
        }
        if (RequestedServiceLevels is not null)
        {
            if (RequestedServiceLevels.Any(x => !Enum.IsDefined(x))) yield return new("Requested service level is invalid.", [nameof(RequestedServiceLevels)]);
            if (RequestedServiceLevels.Distinct().Count() != RequestedServiceLevels.Count) yield return new("Requested service levels must not contain duplicates.", [nameof(RequestedServiceLevels)]);
        }
    }
    private static IEnumerable<ValidationResult> ValidateAddress(QuoteAddressRequest address, string prefix)
    {
        if (string.IsNullOrWhiteSpace(address.CountryCode)) yield return new("CountryCode is required.", [$"{prefix}.CountryCode"]);
        if (string.IsNullOrWhiteSpace(address.City)) yield return new("City is required.", [$"{prefix}.City"]);
    }
}

public sealed record QuoteAddressResponse(string CountryCode, string City, string? StateOrProvince, string? PostalCode, string? AddressLine1);
public sealed record QuoteOptionResponse(Guid QuoteOptionId, Guid CarrierId, string CarrierCode, string CarrierName,
    Guid CarrierServiceId, string ServiceCode, string ServiceName, ServiceLevel ServiceLevel, decimal Amount, string Currency,
    int EstimatedMinDays, int EstimatedMaxDays);
public sealed record ShippingQuoteResponse(Guid QuoteId, QuoteAddressResponse Origin, QuoteAddressResponse Destination,
    string Currency, ShippingQuoteStatus Status, bool IsExpired, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<QuoteOptionResponse> Options);
public sealed record QuoteHistoryResponse(IReadOnlyCollection<ShippingQuoteResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed class ShippingQuoteOptions
{
    public const string SectionName = "ShippingQuotes";
    [Range(1, 10080)] public int LifetimeMinutes { get; set; } = 30;
}

public sealed record CarrierRateAddress(string CountryCode, string City, string? StateOrProvince, string? PostalCode, string? AddressLine1);
public sealed record CarrierRatePackage(decimal Weight, WeightUnit WeightUnit, decimal? Length, decimal? Width, decimal? Height,
    DimensionUnit? DimensionUnit, decimal? DeclaredValue);
public sealed record EligibleCarrierService(string Code, ServiceLevel ServiceLevel, int EstimatedMinDays, int EstimatedMaxDays);
public sealed record CarrierRateRequest(CarrierRateAddress Origin, CarrierRateAddress Destination, IReadOnlyCollection<CarrierRatePackage> Packages,
    string Currency, bool RequiresCod, IReadOnlyCollection<EligibleCarrierService> Services);
public sealed record CarrierRateResult(string ServiceCode, decimal Amount, string Currency, int EstimatedMinDays, int EstimatedMaxDays, string? ProviderReference);
public interface ICarrierRateProvider : ICarrierProvider
{
    Task<IReadOnlyCollection<CarrierRateResult>> GetRatesAsync(CarrierRateRequest request, CancellationToken cancellationToken);
}
public interface IShippingQuoteService
{
    Task<ServiceResult<ShippingQuoteResponse>> CreateAsync(CreateShippingQuoteRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ShippingQuoteResponse>> GetAsync(Guid quoteId, CancellationToken cancellationToken);
    Task<ServiceResult<QuoteHistoryResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
