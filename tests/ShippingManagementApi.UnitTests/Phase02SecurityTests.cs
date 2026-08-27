using ShippingManagementApi.Domain.Merchants;
using ShippingManagementApi.Infrastructure.Identity;

namespace ShippingManagementApi.UnitTests;

public sealed class Phase02SecurityTests
{
    [Fact]
    public void Merchant_NormalizesCodeAndEnforcesInvariants()
    {
        var merchant = new Merchant(" Example Store ", " store-eg_1 ", DateTimeOffset.UtcNow);
        Assert.Equal("Example Store", merchant.Name);
        Assert.Equal("STORE-EG_1", merchant.Code);
        Assert.Throws<ArgumentException>(() => new Merchant("Store", "invalid code", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RefreshToken_HashIsDeterministicAndDoesNotExposeRawToken()
    {
        var raw = RefreshTokenSecurity.Generate();
        var anotherRaw = RefreshTokenSecurity.Generate();
        var hash = RefreshTokenSecurity.Hash(raw);
        Assert.NotEqual(raw, anotherRaw);
        Assert.DoesNotContain(raw, character => character is '+' or '/' or '=');
        Assert.NotEqual(raw, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, RefreshTokenSecurity.Hash(raw));
    }

    [Fact]
    public void RefreshToken_UsabilityHonorsExpirationAndRevocation()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken { ExpiresAtUtc = now.AddMinutes(1) };
        Assert.True(token.IsUsable(now));
        Assert.False(token.IsUsable(now.AddMinutes(2)));
        token.RevokedAtUtc = now;
        Assert.False(token.IsUsable(now));
    }
}
