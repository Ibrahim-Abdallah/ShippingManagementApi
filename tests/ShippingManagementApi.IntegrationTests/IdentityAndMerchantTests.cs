using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.IntegrationTests;

public sealed class IdentityAndMerchantTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Test-Only-Strong1!";

    [Fact]
    public async Task LoginAndMe_EnforceAuthenticationAndSafeFailures()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(email, Password, AppRoles.Admin);
        using var client = factory.CreateClient();

        using var invalid = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.DoesNotContain(email, await invalid.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var anonymous = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var pair = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        using var me = await client.GetAsync("/api/auth/me");
        var body = await me.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InactiveUserAndInactiveMerchant_CannotLogin()
    {
        using var client = factory.CreateClient();
        var inactiveUser = $"inactive-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(inactiveUser, Password, AppRoles.Admin, userActive: false);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new { email = inactiveUser, password = Password })).StatusCode);

        var inactiveMerchant = $"merchant-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(inactiveMerchant, Password, AppRoles.Merchant, "INACTIVE" + Guid.NewGuid().ToString("N"), merchantActive: false);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new { email = inactiveMerchant, password = Password })).StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesRejectsReuseAndStoresOnlyHash()
    {
        var email = $"rotate-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(email, Password, AppRoles.Admin);
        using var client = factory.CreateClient();
        var first = await LoginAsync(client, email);
        Assert.False(await factory.RawRefreshTokenIsPersistedAsync(first.RefreshToken));

        using var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = await ReadPairAsync(refreshed);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task LogoutAndExpiration_PreventFutureRefresh()
    {
        var email = $"logout-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(email, Password, AppRoles.Admin);
        using var client = factory.CreateClient();
        var pair = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = pair.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = pair.RefreshToken })).StatusCode);

        pair = await LoginAsync(client, email);
        await factory.ExpireRefreshTokenAsync(pair.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = pair.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task AdminProvisioningAndMerchantIsolation_AreEnforced()
    {
        var adminEmail = $"provision-admin-{Guid.NewGuid():N}@example.test";
        var merchantEmail = $"existing-merchant-{Guid.NewGuid():N}@example.test";
        var merchantBEmail = $"merchant-b-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(adminEmail, Password, AppRoles.Admin);
        var existing = await factory.CreateUserAsync(merchantEmail, Password, AppRoles.Merchant, "OWN" + Guid.NewGuid().ToString("N"));
        var merchantB = await factory.CreateUserAsync(merchantBEmail, Password, AppRoles.Merchant, "OTHER" + Guid.NewGuid().ToString("N"));
        using var client = factory.CreateClient();

        var merchantPair = await LoginAsync(client, merchantEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantPair.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/merchants", ProvisionBody())).StatusCode);

        var adminPair = await LoginAsync(client, adminEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminPair.AccessToken);
        using var provision = await client.PostAsJsonAsync("/api/admin/merchants", ProvisionBody());
        Assert.Equal(HttpStatusCode.Created, provision.StatusCode);
        var otherId = JsonDocument.Parse(await provision.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantPair.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/merchants/{existing.MerchantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/merchants/{merchantB.MerchantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/merchants/{otherId}")).StatusCode);

        var merchantBPair = await LoginAsync(client, merchantBEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantBPair.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/merchants/{merchantB.MerchantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/merchants/{existing.MerchantId}")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_ContainsBearerSchemeAndAuthRoutes()
    {
        using var client = factory.CreateClient();
        var document = await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/api/auth/refresh", document, StringComparison.Ordinal);
        Assert.Contains("/api/auth/logout", document, StringComparison.Ordinal);
        Assert.Contains("/api/auth/me", document, StringComparison.Ordinal);
        Assert.Contains("bearer", document, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(document);
        Assert.True(json.RootElement.GetProperty("paths").GetProperty("/api/auth/me").GetProperty("get")
            .TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Operator_HasNoMerchantAdministrationAccess()
    {
        var email = $"operator-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(email, Password, AppRoles.Operator);
        using var client = factory.CreateClient();
        var pair = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/admin/merchants", ProvisionBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/merchants/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task OpenApiAndScalar_AreUnavailableInProduction()
    {
        using var productionFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = productionFactory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar/v1")).StatusCode);
    }

    private static object ProvisionBody() => new
    {
        name = "Provisioned Merchant", code = "P" + Guid.NewGuid().ToString("N"),
        initialUserEmail = $"new-{Guid.NewGuid():N}@example.test", initialUserPassword = Password
    };

    private static async Task<TokenPairResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPairAsync(response);
    }

    private static async Task<TokenPairResponse> ReadPairAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TokenPairResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
}
