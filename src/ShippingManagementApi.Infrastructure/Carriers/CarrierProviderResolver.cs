using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Domain.Carriers;
using ShippingManagementApi.Domain.Quotes;
using ShippingManagementApi.Application.Quotes;

namespace ShippingManagementApi.Infrastructure.Carriers;

public sealed class DemoCarrier : ICarrierRateProvider
{
    public const string Code = "DEMO";
    public string CarrierCode => Code;
    public CarrierProviderCapabilities Capabilities { get; } = new(true, true, true, true);

    public Task<IReadOnlyCollection<CarrierRateResult>> GetRatesAsync(CarrierRateRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chargeableKg = request.Packages.Sum(package =>
        {
            var actualKg = package.WeightUnit == WeightUnit.Kg ? package.Weight : package.Weight * 0.45359237m;
            var volumetricKg = 0m;
            if (package.Length.HasValue)
            {
                var multiplier = package.DimensionUnit == DimensionUnit.In ? 2.54m : 1m;
                volumetricKg = package.Length.Value * multiplier * package.Width!.Value * multiplier * package.Height!.Value * multiplier / 5000m;
            }
            return Math.Max(actualKg, volumetricKg);
        });
        var results = request.Services.Select(service =>
        {
            var amount = service.Code.ToUpperInvariant() switch
            {
                "STANDARD" => 8m + chargeableKg * 1.50m,
                "EXPRESS" => 14m + chargeableKg * 2.25m,
                _ => 0m
            };
            return amount <= 0 ? null : new CarrierRateResult(service.Code,
                decimal.Round(amount, 2, MidpointRounding.AwayFromZero), request.Currency,
                service.EstimatedMinDays, service.EstimatedMaxDays, $"DEMO-{service.Code.ToUpperInvariant()}");
        }).Where(x => x is not null).Cast<CarrierRateResult>().ToArray();
        return Task.FromResult<IReadOnlyCollection<CarrierRateResult>>(results);
    }
}

public sealed class CarrierProviderResolver : ICarrierProviderResolver
{
    private readonly IReadOnlyDictionary<string, ICarrierProvider> _providers;

    public CarrierProviderResolver(IEnumerable<ICarrierProvider> providers)
    {
        var normalized = providers.Select(provider => new { Provider = provider, Code = Carrier.NormalizeCode(provider.CarrierCode) }).ToArray();
        var duplicate = normalized.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Multiple carrier providers are registered for code '{duplicate.Key}'.");
        _providers = normalized.ToDictionary(x => x.Code, x => x.Provider, StringComparer.OrdinalIgnoreCase);
    }

    public ICarrierProvider Resolve(string carrierCode) => TryResolve(carrierCode, out var provider)
        ? provider!
        : throw new KeyNotFoundException($"No carrier provider is registered for code '{carrierCode?.Trim()}'.");

    public bool TryResolve(string carrierCode, out ICarrierProvider? provider)
    {
        provider = null;
        try { return _providers.TryGetValue(Carrier.NormalizeCode(carrierCode), out provider); }
        catch (ArgumentException) { return false; }
    }
}
