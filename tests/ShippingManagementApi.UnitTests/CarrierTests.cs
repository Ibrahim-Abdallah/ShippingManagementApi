using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Domain.Carriers;
using ShippingManagementApi.Infrastructure.Carriers;
using ShippingManagementApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ShippingManagementApi.UnitTests;

public sealed class CarrierTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Carrier_NormalizesCodeTrimsNameAndControlsActivation()
    {
        var carrier = new Carrier(" demo-eg_1 ", " Demo Carrier ", true, true, false, true, Now);
        Assert.Equal("DEMO-EG_1", carrier.Code);
        Assert.Equal("Demo Carrier", carrier.Name);
        Assert.True(carrier.IsActive);
        carrier.SetActivation(false, Now.AddMinutes(1));
        Assert.False(carrier.IsActive);
        Assert.Equal(Now.AddMinutes(1), carrier.UpdatedAtUtc);
        carrier.SetActivation(true, Now.AddMinutes(2));
        Assert.True(carrier.IsActive);
    }

    [Fact]
    public void Carrier_RejectsInvalidRequiredAndLengthValues()
    {
        Assert.Throws<ArgumentException>(() => new Carrier("bad code", "Carrier", false, false, false, false, Now));
        Assert.Throws<ArgumentException>(() => new Carrier("CODE", " ", false, false, false, false, Now));
        Assert.Throws<ArgumentException>(() => new Carrier(new string('A', Carrier.MaximumCodeLength + 1), "Carrier", false, false, false, false, Now));
    }

    [Fact]
    public void CarrierService_NormalizesCodeAndSupportsValidRangesIncludingSameDay()
    {
        var carrierId = Guid.NewGuid();
        var standard = new CarrierService(carrierId, " standard ", " Standard ", ServiceLevel.Standard, 2, 5, Now);
        var sameDay = new CarrierService(carrierId, "same_day", "Same Day", ServiceLevel.SameDay, 0, 0, Now);
        Assert.Equal("STANDARD", standard.Code);
        Assert.Equal("Standard", standard.Name);
        Assert.Equal(0, sameDay.EstimatedMinDays);
        standard.SetActivation(false, Now.AddMinutes(1));
        Assert.False(standard.IsActive);
        standard.SetActivation(true, Now.AddMinutes(2));
        Assert.True(standard.IsActive);
    }

    [Fact]
    public void CarrierService_RejectsInvalidRangesAndServiceLevel()
    {
        var carrierId = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() => new CarrierService(carrierId, "CODE", "Name", ServiceLevel.Standard, -1, 2, Now));
        Assert.Throws<ArgumentException>(() => new CarrierService(carrierId, "CODE", "Name", ServiceLevel.Standard, 3, 2, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CarrierService(carrierId, "CODE", "Name", (ServiceLevel)99, 1, 2, Now));
    }

    [Fact]
    public void Resolver_ResolvesDemoUsingNormalizedCodeAndRejectsUnknown()
    {
        var resolver = new CarrierProviderResolver([new DemoCarrier()]);
        Assert.IsType<DemoCarrier>(resolver.Resolve(" demo "));
        Assert.True(resolver.TryResolve("DEMO", out var provider));
        Assert.Equal(DemoCarrier.Code, provider!.CarrierCode);
        Assert.False(resolver.TryResolve("UNKNOWN", out _));
        Assert.Throws<KeyNotFoundException>(() => resolver.Resolve("UNKNOWN"));
    }

    [Fact]
    public void Resolver_RejectsDuplicateNormalizedProviderCodes()
    {
        Assert.Throws<InvalidOperationException>(() => new CarrierProviderResolver([new DemoCarrier(), new StubProvider("demo")]));
    }

    [Fact]
    public void SqlServerClassifier_DoesNotTreatUnrelatedDatabaseUpdateFailureAsDuplicate()
    {
        var exception = new DbUpdateException("A non-unique persistence failure occurred.", new InvalidOperationException("Database unavailable."));

        Assert.False(SqlServerDatabaseErrorClassifier.IsUniqueConstraintViolation(exception));
    }

    [Theory]
    [InlineData(2601, true)]
    [InlineData(2627, true)]
    [InlineData(547, false)]
    public void SqlServerClassifier_RecognizesOnlyDuplicateErrorNumbers(int errorNumber, bool expected)
    {
        Assert.Equal(expected, SqlServerDatabaseErrorClassifier.IsUniqueConstraintViolationNumber(errorNumber));
    }

    private sealed class StubProvider(string code) : ICarrierProvider
    {
        public string CarrierCode => code;
        public CarrierProviderCapabilities Capabilities { get; } = new(false, false, false, false);
    }
}
