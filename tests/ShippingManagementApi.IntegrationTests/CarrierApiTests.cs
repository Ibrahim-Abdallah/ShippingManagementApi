using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.IntegrationTests;

public sealed class CarrierApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Test-Only-Strong1!";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task AdminEndpoints_RequireAdminWhileMerchantCanReadCatalog()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/carriers")).StatusCode);

        var merchantEmail = $"carrier-merchant-{Guid.NewGuid():N}@example.test";
        var operatorEmail = $"carrier-operator-{Guid.NewGuid():N}@example.test";
        var adminEmail = $"carrier-admin-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(merchantEmail, Password, AppRoles.Merchant, "CAT" + Guid.NewGuid().ToString("N"));
        await factory.CreateUserAsync(operatorEmail, Password, AppRoles.Operator);
        await factory.CreateUserAsync(adminEmail, Password, AppRoles.Admin);

        await AuthorizeAsync(client, merchantEmail);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/carriers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/carriers", CarrierBody("MERCHANT"))).StatusCode);

        await AuthorizeAsync(client, operatorEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/carriers", CarrierBody("OPERATOR"))).StatusCode);

        await AuthorizeAsync(client, adminEmail);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/admin/carriers", CarrierBody("ADMIN-OK"))).StatusCode);
    }

    [Fact]
    public async Task CarrierAndServiceManagement_EnforcesNormalizationConflictsOwnershipAndSafeDelete()
    {
        using var client = factory.CreateClient();
        await CreateAndAuthorizeAdminAsync(client);
        var first = await CreateCarrierAsync(client, "  test-carrier  ");
        var second = await CreateCarrierAsync(client, "SECOND-" + Guid.NewGuid().ToString("N"));
        Assert.Equal("TEST-CARRIER", first.Code);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/carriers", CarrierBody("test-carrier"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/carriers/{first.Id}")).StatusCode);

        using var update = await client.PutAsJsonAsync($"/api/admin/carriers/{first.Id}", new
        {
            name = "Updated Carrier", supportsPickup = false, supportsTracking = true, supportsCancellation = true, supportsCod = false
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<CarrierResponse>();
        Assert.Equal(first.Code, updated!.Code);
        Assert.Equal("Updated Carrier", updated.Name);

        var service = await CreateServiceAsync(client, first.Id, " standard ");
        Assert.Equal("STANDARD", service.Code);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/admin/carriers/{first.Id}/services", ServiceBody("standard"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/admin/carriers/{second.Id}/services", ServiceBody("STANDARD"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/carriers/{second.Id}/services/{service.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/carriers/{first.Id}/services", new
        {
            code = "INVALID", name = "Invalid", serviceLevel = "Express", estimatedMinDays = 4, estimatedMaxDays = 2
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/admin/carriers/{first.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/admin/carriers/{first.Id}/activation", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/admin/carriers/{first.Id}/activation", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/admin/carriers/{first.Id}/services/{service.Id}/activation", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/admin/carriers/{first.Id}/services/{service.Id}/activation", new { isActive = true })).StatusCode);
    }

    [Fact]
    public async Task ActiveCatalog_FiltersDisabledServicesAndCarriersAndRestoresThemWhenReenabled()
    {
        using var client = factory.CreateClient();
        await CreateAndAuthorizeAdminAsync(client);
        var carrier = await CreateCarrierAsync(client, "CATALOG-" + Guid.NewGuid().ToString("N"));
        var service = await CreateServiceAsync(client, carrier.Id, "EXPRESS");

        Assert.Contains(await ReadCatalogAsync(client), x => x.Id == carrier.Id && x.Services.Any(s => s.Id == service.Id));
        await client.PatchAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services/{service.Id}/activation", new { isActive = false });
        Assert.True((await ReadCatalogAsync(client)).Single(x => x.Id == carrier.Id).Services.All(x => x.Id != service.Id));
        await client.PatchAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services/{service.Id}/activation", new { isActive = true });
        Assert.Contains((await ReadCatalogAsync(client)).Single(x => x.Id == carrier.Id).Services, x => x.Id == service.Id);

        await client.PatchAsJsonAsync($"/api/admin/carriers/{carrier.Id}/activation", new { isActive = false });
        Assert.DoesNotContain(await ReadCatalogAsync(client), x => x.Id == carrier.Id);
        await client.PatchAsJsonAsync($"/api/admin/carriers/{carrier.Id}/activation", new { isActive = true });
        Assert.Contains(await ReadCatalogAsync(client), x => x.Id == carrier.Id);
    }

    [Fact]
    public async Task DemoCatalogSeed_ExistsAndProviderResolvesMatchingCode()
    {
        using var client = factory.CreateClient();
        await CreateAndAuthorizeAdminAsync(client);
        var carriers = await client.GetFromJsonAsync<CarrierResponse[]>("/api/admin/carriers");
        var demo = Assert.Single(carriers!, x => x.Code == "DEMO");
        var services = await client.GetFromJsonAsync<CarrierServiceResponse[]>($"/api/admin/carriers/{demo.Id}/services", JsonOptions);
        Assert.Contains(services!, x => x.Code == "STANDARD" && x.ServiceLevel == ShippingManagementApi.Domain.Carriers.ServiceLevel.Standard);
        Assert.Contains(services!, x => x.Code == "EXPRESS" && x.ServiceLevel == ShippingManagementApi.Domain.Carriers.ServiceLevel.Express);

        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICarrierProviderResolver>();
        Assert.Equal(demo.Code, resolver.Resolve("demo").CarrierCode);
        var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
        Assert.True(await db.Carriers.AnyAsync(x => x.Code == resolver.Resolve("DEMO").CarrierCode));
    }

    [Fact]
    public async Task OpenApi_ContainsCarrierRoutesStringServiceLevelsAndBearerRequirements()
    {
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/admin/carriers", out var adminCarriers));
        Assert.True(paths.TryGetProperty("/api/admin/carriers/{carrierId}/services/{serviceId}/activation", out _));
        Assert.True(paths.TryGetProperty("/api/carriers", out var catalog));
        Assert.True(adminCarriers.GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.True(catalog.GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.Contains("Standard", json.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MutationRequests_RequireExplicitActivationAndServiceValuesWhileAllowingFalseAndZero()
    {
        using var client = factory.CreateClient();
        await CreateAndAuthorizeAdminAsync(client);
        var carrier = await CreateCarrierAsync(client, "REQUIRED-" + Guid.NewGuid().ToString("N"));
        var service = await CreateServiceAsync(client, carrier.Id, "STANDARD");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync($"/api/admin/carriers/{carrier.Id}/activation", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services/{service.Id}/activation", new { })).StatusCode);

        using var explicitFalse = await client.PatchAsJsonAsync(
            $"/api/admin/carriers/{carrier.Id}/activation", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, explicitFalse.StatusCode);
        Assert.False((await explicitFalse.Content.ReadFromJsonAsync<CarrierResponse>())!.IsActive);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services", new
            {
                code = "NO-LEVEL", name = "Missing Level", estimatedMinDays = 0, estimatedMaxDays = 0
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services", new
            {
                code = "NO-MIN", name = "Missing Minimum", serviceLevel = "SameDay", estimatedMaxDays = 0
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services", new
            {
                code = "NO-MAX", name = "Missing Maximum", serviceLevel = "SameDay", estimatedMinDays = 0
            })).StatusCode);

        using var sameDay = await client.PostAsJsonAsync($"/api/admin/carriers/{carrier.Id}/services", new
        {
            code = "SAME-DAY", name = "Same Day", serviceLevel = "SameDay", estimatedMinDays = 0, estimatedMaxDays = 0
        });
        Assert.Equal(HttpStatusCode.Created, sameDay.StatusCode);
        var sameDayResponse = await sameDay.Content.ReadFromJsonAsync<CarrierServiceResponse>(JsonOptions);
        Assert.Equal(0, sameDayResponse!.EstimatedMinDays);
        Assert.Equal(0, sameDayResponse.EstimatedMaxDays);
    }

    private static object CarrierBody(string code) => new { code, name = "Test Carrier", supportsPickup = true, supportsTracking = true, supportsCancellation = true, supportsCod = false };
    private static object ServiceBody(string code) => new { code, name = "Standard Service", serviceLevel = "Standard", estimatedMinDays = 2, estimatedMaxDays = 5 };
    private static async Task<CarrierResponse> CreateCarrierAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/carriers", CarrierBody(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CarrierResponse>())!;
    }
    private static async Task<CarrierServiceResponse> CreateServiceAsync(HttpClient client, Guid carrierId, string code)
    {
        using var response = await client.PostAsJsonAsync($"/api/admin/carriers/{carrierId}/services", ServiceBody(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CarrierServiceResponse>(JsonOptions))!;
    }
    private static async Task<CarrierCatalogResponse[]> ReadCatalogAsync(HttpClient client) => (await client.GetFromJsonAsync<CarrierCatalogResponse[]>("/api/carriers", JsonOptions))!;
    private async Task CreateAndAuthorizeAdminAsync(HttpClient client)
    {
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(email, Password, AppRoles.Admin);
        await AuthorizeAsync(client, email);
    }
    private static async Task AuthorizeAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
    }
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
