using ShippingManagementApi.Domain.Carriers;

namespace ShippingManagementApi.Domain.Quotes;

public enum WeightUnit { Kg, Lb }
public enum DimensionUnit { Cm, In }
public enum ShippingQuoteStatus { Active, Expired }

public sealed class QuoteAddress
{
    public const int MaximumCountryCodeLength = 2, MaximumCityLength = 100, MaximumStateLength = 100,
        MaximumPostalCodeLength = 20, MaximumAddressLineLength = 200;
    private QuoteAddress() { }
    public QuoteAddress(string countryCode, string city, string? stateOrProvince, string? postalCode, string? addressLine1)
    {
        CountryCode = NormalizeCountry(countryCode);
        City = Required(city, MaximumCityLength, nameof(city));
        StateOrProvince = Optional(stateOrProvince, MaximumStateLength, nameof(stateOrProvince));
        PostalCode = Optional(postalCode, MaximumPostalCodeLength, nameof(postalCode));
        AddressLine1 = Optional(addressLine1, MaximumAddressLineLength, nameof(addressLine1));
    }
    public string CountryCode { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? StateOrProvince { get; private set; }
    public string? PostalCode { get; private set; }
    public string? AddressLine1 { get; private set; }
    private static string NormalizeCountry(string value)
    {
        var normalized = Required(value, 2, nameof(value)).ToUpperInvariant();
        if (normalized.Length != 2 || normalized.Any(c => !char.IsAsciiLetter(c)))
            throw new ArgumentException("Country code must contain exactly two alphabetic characters.", nameof(value));
        return normalized;
    }
    private static string Required(string value, int max, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return result;
    }
    private static string? Optional(string? value, int max, string name)
    {
        if (value is null) return null;
        var result = value.Trim();
        if (result.Length == 0) return null;
        if (result.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return result;
    }
}

public sealed class ShippingQuotePackage
{
    public const decimal MaximumWeight = 1000m, MaximumDimension = 1000m, MaximumDeclaredValue = 100000000m;
    private ShippingQuotePackage() { }
    public ShippingQuotePackage(decimal weight, WeightUnit weightUnit, decimal? length, decimal? width,
        decimal? height, DimensionUnit? dimensionUnit, decimal? declaredValue)
    {
        if (weight <= 0 || weight > MaximumWeight) throw new ArgumentOutOfRangeException(nameof(weight), $"Weight must be greater than zero and at most {MaximumWeight}.");
        if (!Enum.IsDefined(weightUnit)) throw new ArgumentOutOfRangeException(nameof(weightUnit));
        var supplied = new[] { length.HasValue, width.HasValue, height.HasValue, dimensionUnit.HasValue };
        if (supplied.Any(x => x) && !supplied.All(x => x)) throw new ArgumentException("Length, width, height, and dimension unit must be supplied together.");
        if (length is <= 0 or > MaximumDimension || width is <= 0 or > MaximumDimension || height is <= 0 or > MaximumDimension)
            throw new ArgumentOutOfRangeException(nameof(length), $"Dimensions must be greater than zero and at most {MaximumDimension}.");
        if (dimensionUnit.HasValue && !Enum.IsDefined(dimensionUnit.Value)) throw new ArgumentOutOfRangeException(nameof(dimensionUnit));
        if (declaredValue is < 0 or > MaximumDeclaredValue) throw new ArgumentOutOfRangeException(nameof(declaredValue), "Declared value must not be negative or unreasonably large.");
        Id = Guid.NewGuid(); Weight = weight; WeightUnit = weightUnit; Length = length; Width = width; Height = height;
        DimensionUnit = dimensionUnit; DeclaredValue = declaredValue;
    }
    public Guid Id { get; private set; }
    public Guid ShippingQuoteId { get; private set; }
    public decimal Weight { get; private set; }
    public WeightUnit WeightUnit { get; private set; }
    public decimal? Length { get; private set; }
    public decimal? Width { get; private set; }
    public decimal? Height { get; private set; }
    public DimensionUnit? DimensionUnit { get; private set; }
    public decimal? DeclaredValue { get; private set; }
}

public sealed class QuoteOption
{
    public const int MaximumCodeLength = 50, MaximumNameLength = 200, MaximumCurrencyLength = 3, MaximumProviderReferenceLength = 200;
    private QuoteOption() { }
    public QuoteOption(Guid carrierId, string carrierCode, string carrierName, Guid carrierServiceId, string serviceCode,
        string serviceName, ServiceLevel serviceLevel, decimal amount, string currency, int estimatedMinDays,
        int estimatedMaxDays, string? providerReference)
    {
        if (carrierId == Guid.Empty || carrierServiceId == Guid.Empty) throw new ArgumentException("Carrier and service identifiers are required.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Option amount must be greater than zero.");
        if (estimatedMinDays < 0 || estimatedMaxDays < estimatedMinDays) throw new ArgumentException("Estimated delivery range is invalid.");
        if (!Enum.IsDefined(serviceLevel)) throw new ArgumentOutOfRangeException(nameof(serviceLevel));
        Id = Guid.NewGuid(); CarrierId = carrierId; CarrierCode = Required(carrierCode, MaximumCodeLength);
        CarrierName = Required(carrierName, MaximumNameLength); CarrierServiceId = carrierServiceId;
        ServiceCode = Required(serviceCode, MaximumCodeLength); ServiceName = Required(serviceName, MaximumNameLength);
        ServiceLevel = serviceLevel; Amount = amount; Currency = ShippingQuote.NormalizeCurrency(currency);
        EstimatedMinDays = estimatedMinDays; EstimatedMaxDays = estimatedMaxDays;
        ProviderReference = Optional(providerReference, MaximumProviderReferenceLength);
    }
    public Guid Id { get; private set; }
    public Guid ShippingQuoteId { get; private set; }
    public Guid CarrierId { get; private set; }
    public string CarrierCode { get; private set; } = string.Empty;
    public string CarrierName { get; private set; } = string.Empty;
    public Guid CarrierServiceId { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string ServiceName { get; private set; } = string.Empty;
    public ServiceLevel ServiceLevel { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public int EstimatedMinDays { get; private set; }
    public int EstimatedMaxDays { get; private set; }
    public string? ProviderReference { get; private set; }
    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Snapshot value is too long."); return v; }
    private static string? Optional(string? value, int max) { var v = value?.Trim(); if (v?.Length > max) throw new ArgumentException("Provider reference is too long."); return string.IsNullOrEmpty(v) ? null : v; }
}

public sealed class ShippingQuote
{
    public const int MaximumPackages = 50;
    private readonly List<ShippingQuotePackage> _packages = [];
    private readonly List<QuoteOption> _options = [];
    private ShippingQuote() { }
    public ShippingQuote(Guid merchantId, QuoteAddress origin, QuoteAddress destination, string currency,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc, IEnumerable<ShippingQuotePackage> packages, IEnumerable<QuoteOption> options)
    {
        if (merchantId == Guid.Empty) throw new ArgumentException("Merchant identifier is required.", nameof(merchantId));
        if (expiresAtUtc <= createdAtUtc) throw new ArgumentException("Quote expiration must be after creation.", nameof(expiresAtUtc));
        var packageList = packages?.ToList() ?? throw new ArgumentNullException(nameof(packages));
        var optionList = options?.ToList() ?? throw new ArgumentNullException(nameof(options));
        if (packageList.Count is < 1 or > MaximumPackages) throw new ArgumentException($"A quote requires between 1 and {MaximumPackages} packages.", nameof(packages));
        if (optionList.Count == 0) throw new ArgumentException("A quote requires at least one option.", nameof(options));
        if (optionList.Select(x => x.CarrierServiceId).Distinct().Count() != optionList.Count) throw new ArgumentException("A quote cannot contain duplicate service options.", nameof(options));
        Id = Guid.NewGuid(); MerchantId = merchantId; Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        Destination = destination ?? throw new ArgumentNullException(nameof(destination)); Currency = NormalizeCurrency(currency);
        CreatedAtUtc = createdAtUtc; ExpiresAtUtc = expiresAtUtc;
        _packages.AddRange(packageList); _options.AddRange(optionList);
    }
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public QuoteAddress Origin { get; private set; } = null!;
    public QuoteAddress Destination { get; private set; } = null!;
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ShippingQuotePackage> Packages => _packages.AsReadOnly();
    public IReadOnlyCollection<QuoteOption> Options => _options.AsReadOnly();
    public bool IsExpired(TimeProvider timeProvider) => timeProvider.GetUtcNow() >= ExpiresAtUtc;
    public ShippingQuoteStatus GetStatus(TimeProvider timeProvider) => IsExpired(timeProvider) ? ShippingQuoteStatus.Expired : ShippingQuoteStatus.Active;
    public static string NormalizeCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var value = currency.Trim().ToUpperInvariant();
        if (value.Length != 3 || value.Any(c => !char.IsAsciiLetter(c))) throw new ArgumentException("Currency must contain exactly three alphabetic characters.", nameof(currency));
        return value;
    }
}
