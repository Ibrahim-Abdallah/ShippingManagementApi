using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Domain.Carriers;

namespace ShippingManagementApi.Infrastructure.Carriers;

public sealed class DemoCarrier : ICarrierProvider
{
    public const string Code = "DEMO";
    public string CarrierCode => Code;
    public CarrierProviderCapabilities Capabilities { get; } = new(true, true, true, true);
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
