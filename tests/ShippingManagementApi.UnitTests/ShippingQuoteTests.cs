using ShippingManagementApi.Application.Quotes;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Domain.Carriers;
using ShippingManagementApi.Domain.Quotes;
using ShippingManagementApi.Infrastructure.Carriers;

namespace ShippingManagementApi.UnitTests;

public sealed class ShippingQuoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Quote_ExpirationBoundaryUsesTimeAndRequiresFutureExpirationAndOptions()
    {
        var quote = CreateQuote(Now.AddMinutes(30));
        Assert.Equal(ShippingQuoteStatus.Active, quote.GetStatus(new FixedTimeProvider(Now.AddMinutes(29))));
        Assert.Equal(ShippingQuoteStatus.Expired, quote.GetStatus(new FixedTimeProvider(Now.AddMinutes(30))));
        Assert.Equal(ShippingQuoteStatus.Expired, quote.GetStatus(new FixedTimeProvider(Now.AddMinutes(31))));
        Assert.Throws<ArgumentException>(() => CreateQuote(Now));
        Assert.Throws<ArgumentException>(() => new ShippingQuote(Guid.NewGuid(), Address(), Address(), "USD", Now,
            Now.AddMinutes(1), [Package()], []));
    }

    [Fact]
    public void AddressAndCurrencyNormalizeAndRejectInvalidValues()
    {
        var address = new QuoteAddress(" eg ", " Cairo ", " Giza ", null, " Street ");
        Assert.Equal("EG", address.CountryCode); Assert.Equal("Cairo", address.City); Assert.Equal("Giza", address.StateOrProvince);
        Assert.Equal("USD", ShippingQuote.NormalizeCurrency(" usd "));
        Assert.Throws<ArgumentException>(() => new QuoteAddress("EGY", "Cairo", null, null, null));
        Assert.Throws<ArgumentException>(() => ShippingQuote.NormalizeCurrency("US1"));
    }

    [Fact]
    public void PackageValidatesWeightDimensionsUnitsAndDeclaredValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingQuotePackage(0, WeightUnit.Kg, null, null, null, null, null));
        Assert.Throws<ArgumentException>(() => new ShippingQuotePackage(1, WeightUnit.Kg, 10, null, 10, DimensionUnit.Cm, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingQuotePackage(1, WeightUnit.Kg, 0, 10, 10, DimensionUnit.Cm, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingQuotePackage(1, WeightUnit.Kg, null, null, null, null, -1));
        Assert.Equal(0, new ShippingQuotePackage(1, WeightUnit.Kg, null, null, null, null, 0).DeclaredValue);
    }

    [Fact]
    public void QuoteOptionValidatesPriceRangeAndDuplicateServices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Option(amount: 0));
        Assert.Throws<ArgumentException>(() => Option(min: 5, max: 2));
        var option = Option();
        Assert.Throws<ArgumentException>(() => new ShippingQuote(Guid.NewGuid(), Address(), Address(), "USD", Now,
            Now.AddMinutes(1), [Package()], [option, Option(serviceId: option.CarrierServiceId)]));
    }

    [Fact]
    public void Quote_CollectionsExposeReadOnlyWrappersAndDefensivelyCopyConstructionInputs()
    {
        var package = Package(); var option = Option();
        var packages = new List<ShippingQuotePackage> { package };
        var options = new List<QuoteOption> { option };
        var quote = new ShippingQuote(Guid.NewGuid(), Address(), Address(), "USD", Now, Now.AddMinutes(30), packages, options);

        Assert.Equal(typeof(IReadOnlyCollection<ShippingQuotePackage>), typeof(ShippingQuote).GetProperty(nameof(ShippingQuote.Packages))!.PropertyType);
        Assert.Equal(typeof(IReadOnlyCollection<QuoteOption>), typeof(ShippingQuote).GetProperty(nameof(ShippingQuote.Options))!.PropertyType);
        var exposedPackages = Assert.IsAssignableFrom<ICollection<ShippingQuotePackage>>(quote.Packages);
        var exposedOptions = Assert.IsAssignableFrom<ICollection<QuoteOption>>(quote.Options);
        Assert.True(exposedPackages.IsReadOnly); Assert.True(exposedOptions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposedPackages.Add(Package()));
        Assert.Throws<NotSupportedException>(() => exposedOptions.Clear());

        packages.Clear(); options.Clear();
        Assert.Single(quote.Packages); Assert.Same(package, quote.Packages.Single());
        Assert.Single(quote.Options); Assert.Same(option, quote.Options.Single());
    }

    [Fact]
    public async Task DemoCarrierProducesDeterministicStandardExpressAndVolumetricRates()
    {
        var provider = new DemoCarrier();
        var services = new[] { new EligibleCarrierService("STANDARD", ServiceLevel.Standard, 2, 5), new EligibleCarrierService("EXPRESS", ServiceLevel.Express, 1, 2) };
        var request = new CarrierRateRequest(RateAddress(), RateAddress(), [new(2, WeightUnit.Kg, null, null, null, null, null)], "USD", false, services);
        var rates = await provider.GetRatesAsync(request, default);
        Assert.Equal(11.00m, rates.Single(x => x.ServiceCode == "STANDARD").Amount);
        Assert.Equal(18.50m, rates.Single(x => x.ServiceCode == "EXPRESS").Amount);

        var volumetric = await provider.GetRatesAsync(request with
        {
            Packages = [new CarrierRatePackage(1, WeightUnit.Kg, 50, 40, 30, DimensionUnit.Cm, null)]
        }, default);
        Assert.Equal(26.00m, volumetric.Single(x => x.ServiceCode == "STANDARD").Amount);
        Assert.Equal(41.00m, volumetric.Single(x => x.ServiceCode == "EXPRESS").Amount);
    }

    [Fact]
    public async Task DemoCarrierNormalizesPoundsAndInchesAndImplementsRateCapability()
    {
        ICarrierProvider provider = new DemoCarrier();
        var rateProvider = Assert.IsAssignableFrom<ICarrierRateProvider>(provider);
        var rates = await rateProvider.GetRatesAsync(new(RateAddress(), RateAddress(),
            [new(2.20462262185m, WeightUnit.Lb, null, null, null, null, null)], "EUR", false,
            [new("STANDARD", ServiceLevel.Standard, 2, 5)]), default);
        Assert.Equal(9.50m, Assert.Single(rates).Amount);
    }

    private static ShippingQuote CreateQuote(DateTimeOffset expires) => new(Guid.NewGuid(), Address(), Address(), "USD", Now, expires, [Package()], [Option()]);
    private static QuoteAddress Address() => new("EG", "Cairo", null, null, null);
    private static CarrierRateAddress RateAddress() => new("EG", "Cairo", null, null, null);
    private static ShippingQuotePackage Package() => new(1, WeightUnit.Kg, null, null, null, null, null);
    private static QuoteOption Option(decimal amount = 10, int min = 1, int max = 2, Guid? serviceId = null) =>
        new(Guid.NewGuid(), "DEMO", "Demo", serviceId ?? Guid.NewGuid(), "STANDARD", "Standard", ServiceLevel.Standard, amount, "USD", min, max, "private");
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
