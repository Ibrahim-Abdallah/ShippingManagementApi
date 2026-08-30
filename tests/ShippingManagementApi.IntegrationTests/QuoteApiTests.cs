using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShippingManagementApi.Application.Quotes;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Carriers;
using ShippingManagementApi.Infrastructure.Carriers;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.IntegrationTests;

public sealed class QuoteApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Test-Only-Strong1!";
    private static readonly JsonSerializerOptions JsonOptions = Options();

    [Fact]
    public async Task MerchantCreatesRetrievesAndListsPersistedDemoQuoteWithoutPrivateProviderReference()
    {
        using var client = factory.CreateClient();
        var first = await CreateMerchantAndAuthorize(client);
        using var response = await client.PostAsJsonAsync("/api/quotes", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("providerReference", raw, StringComparison.OrdinalIgnoreCase);
        var quote = JsonSerializer.Deserialize<ShippingQuoteResponse>(raw, JsonOptions)!;
        Assert.Equal("USD", quote.Currency); Assert.Equal("EG", quote.Origin.CountryCode);
        Assert.Contains(quote.Options, x => x.ServiceCode == "STANDARD" && x.Amount == 11m);
        Assert.Contains(quote.Options, x => x.ServiceCode == "EXPRESS" && x.Amount == 18.5m);
        Assert.Equal(response.Headers.Location, new Uri($"/api/quotes/{quote.QuoteId}", UriKind.Relative));

        var retrieved = await client.GetFromJsonAsync<ShippingQuoteResponse>($"/api/quotes/{quote.QuoteId}", JsonOptions);
        Assert.Equal(2, retrieved!.Options.Count);
        var history = await client.GetFromJsonAsync<QuoteHistoryResponse>("/api/quotes?page=1&pageSize=1", JsonOptions);
        Assert.Equal(1, history!.PageSize); Assert.True(history.TotalCount >= 1); Assert.Single(history.Items);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
        var persisted = await db.ShippingQuotes.Include(x => x.Packages).Include(x => x.Options).SingleAsync(x => x.Id == quote.QuoteId);
        Assert.Equal(first.MerchantId, persisted.MerchantId); Assert.Single(persisted.Packages); Assert.Equal(2, persisted.Options.Count);
    }

    [Fact]
    public async Task QuoteEndpointsEnforceAuthenticationMerchantRoleAndOwnershipAsNotFound()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/quotes", ValidRequest())).StatusCode);
        var first = await CreateMerchantAndAuthorize(client);
        var quote = await PostQuote(client);
        var secondEmail = $"quote-second-{Guid.NewGuid():N}@example.test";
        var second = await factory.CreateUserAsync(secondEmail, Password, AppRoles.Merchant, "Q2" + Guid.NewGuid().ToString("N"));
        await Authorize(client, secondEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/quotes/{quote.QuoteId}")).StatusCode);
        var history = await client.GetFromJsonAsync<QuoteHistoryResponse>("/api/quotes", JsonOptions);
        Assert.DoesNotContain(history!.Items, x => x.QuoteId == quote.QuoteId);
        Assert.NotEqual(first.MerchantId, second.MerchantId);

        var adminEmail = $"quote-admin-{Guid.NewGuid():N}@example.test";
        await factory.CreateUserAsync(adminEmail, Password, AppRoles.Admin); await Authorize(client, adminEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/quotes")).StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidQuoteRequestsReturnBadRequest(object request)
    {
        using var client = factory.CreateClient(); await CreateMerchantAndAuthorize(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/quotes", request)).StatusCode);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [new { origin = Address(), destination = Address(), packages = Array.Empty<object>(), currency = "USD" }];
        yield return [new { origin = Address(), destination = Address(), packages = new[] { Package(0) }, currency = "USD" }];
        yield return [new { origin = Address(), destination = Address(), packages = new[] { new { weight = 1, weightUnit = "Kg", length = 1, width = 1 } }, currency = "USD" }];
        yield return [new { origin = Address(), destination = Address(), packages = new[] { new { weightUnit = "Kg" } }, currency = "USD" }];
        yield return [new { origin = Address(), destination = Address(), packages = new[] { new { weight = 1 } }, currency = "USD" }];
        yield return [new { origin = Address(), destination = Address(), packages = new[] { Package(1) }, currency = "12$" }];
        yield return [new { origin = new { countryCode = "EGY", city = "Cairo" }, destination = Address(), packages = new[] { Package(1) }, currency = "USD" }];
    }

    [Theory]
    [InlineData("weightUnit", "Stone")]
    [InlineData("dimensionUnit", "Meter")]
    public async Task InvalidPackageEnumInJsonReturnsSafeBadRequest(string propertyName, string invalidValue)
    {
        using var client = factory.CreateClient(); await CreateMerchantAndAuthorize(client);
        var package = propertyName == "weightUnit"
            ? $$"""{"weight":2,"weightUnit":"{{invalidValue}}"}"""
            : $$"""{"weight":2,"weightUnit":"Kg","length":10,"width":10,"height":10,"dimensionUnit":"{{invalidValue}}"}""";
        await AssertInvalidBodyAsync(client, RequestJson(package));
    }

    [Fact]
    public async Task InvalidRequestedServiceLevelInJsonReturnsSafeBadRequest()
    {
        using var client = factory.CreateClient(); await CreateMerchantAndAuthorize(client);
        await AssertInvalidBodyAsync(client, RequestJson("{\"weight\":2,\"weightUnit\":\"Kg\"}", ",\"requestedServiceLevels\":[\"Teleport\"]"));
    }

    [Fact]
    public async Task MalformedJsonReturnsSafeBadRequest()
    {
        using var client = factory.CreateClient(); await CreateMerchantAndAuthorize(client);
        await AssertInvalidBodyAsync(client, "{\"origin\":{\"countryCode\":\"EG\"");
    }

    [Fact]
    public async Task EligibilityFiltersServiceLevelsAndDisabledConfigurationWithoutMutatingHistory()
    {
        using var client = factory.CreateClient(); await CreateMerchantAndAuthorize(client);
        var filtered = await PostQuote(client, new { origin = Address(), destination = Address(), packages = new[] { Package(2) }, currency = "USD", requestedServiceLevels = new[] { "Express" } });
        Assert.Single(filtered.Options); Assert.Equal("EXPRESS", filtered.Options.Single().ServiceCode);

        Guid standardId, carrierId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
            carrierId = (await db.Carriers.SingleAsync(x => x.Code == DemoCarrier.Code)).Id;
            standardId = (await db.CarrierServices.SingleAsync(x => x.CarrierId == carrierId && x.Code == "STANDARD")).Id;
            await db.CarrierServices.Where(x => x.Id == standardId).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsActive, false));
        }
        try
        {
            var withoutStandard = await PostQuote(client);
            Assert.DoesNotContain(withoutStandard.Options, x => x.ServiceCode == "STANDARD");
            Assert.Equal("EXPRESS", Assert.Single(withoutStandard.Options).ServiceCode);
            Assert.Single((await client.GetFromJsonAsync<ShippingQuoteResponse>($"/api/quotes/{filtered.QuoteId}", JsonOptions))!.Options);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
            await db.Carriers.Where(x => x.Id == carrierId).ExecuteUpdateAsync(x => x.SetProperty(c => c.IsActive, false));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", ValidRequest())).StatusCode);
            await db.Carriers.Where(x => x.Id == carrierId).ExecuteUpdateAsync(x => x.SetProperty(c => c.IsActive, true));
            await db.Carriers.Where(x => x.Id == carrierId).ExecuteUpdateAsync(x => x.SetProperty(c => c.SupportsCod, false));
            var codRequest = new { origin = Address(), destination = Address(), packages = new[] { Package(2) }, currency = "USD", requiresCod = true };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", codRequest)).StatusCode);
            await db.Carriers.Where(x => x.Id == carrierId).ExecuteUpdateAsync(x => x.SetProperty(c => c.SupportsCod, true));
        }
        finally
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
            await db.Carriers.Where(x => x.Id == carrierId).ExecuteUpdateAsync(x => x.SetProperty(c => c.IsActive, true));
            await db.Carriers.Where(x => x.Id == carrierId).ExecuteUpdateAsync(x => x.SetProperty(c => c.SupportsCod, true));
            await db.CarrierServices.Where(x => x.Id == standardId).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsActive, true));
        }
    }

    [Fact]
    public async Task ExpiredQuoteIsComputedAndOpenApiDocumentsMerchantBearerEndpoints()
    {
        using var client = factory.CreateClient(); await CreateMerchantAndAuthorize(client); var quote = await PostQuote(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
            var created = DateTimeOffset.UtcNow.AddHours(-2); var expires = created.AddMinutes(30);
            await db.ShippingQuotes.Where(x => x.Id == quote.QuoteId).ExecuteUpdateAsync(x => x
                .SetProperty(q => q.CreatedAtUtc, created).SetProperty(q => q.ExpiresAtUtc, expires));
        }
        var expired = await client.GetFromJsonAsync<ShippingQuoteResponse>($"/api/quotes/{quote.QuoteId}", JsonOptions);
        Assert.True(expired!.IsExpired); Assert.Equal(ShippingManagementApi.Domain.Quotes.ShippingQuoteStatus.Expired, expired.Status);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var path = json.RootElement.GetProperty("paths").GetProperty("/api/quotes");
        Assert.True(path.GetProperty("post").GetProperty("security").GetArrayLength() > 0);
    }

    private static object ValidRequest() => new { origin = Address(), destination = new { countryCode = "US", city = "New York" }, packages = new[] { Package(2) }, currency = " usd ", requiresCod = false };
    private static object Address() => new { countryCode = " eg ", city = " Cairo ", addressLine1 = "Street" };
    private static object Package(decimal weight) => new { weight, weightUnit = "Kg", declaredValue = 0 };
    private async Task<(Guid UserId, Guid? MerchantId)> CreateMerchantAndAuthorize(HttpClient client)
    {
        var email = $"quote-{Guid.NewGuid():N}@example.test"; var user = await factory.CreateUserAsync(email, Password, AppRoles.Merchant, "Q" + Guid.NewGuid().ToString("N"));
        await Authorize(client, email); return user;
    }
    private static async Task Authorize(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
    }
    private static async Task<ShippingQuoteResponse> PostQuote(HttpClient client, object? request = null)
    {
        using var response = await client.PostAsJsonAsync("/api/quotes", request ?? ValidRequest()); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions))!;
    }
    private static async Task AssertInvalidBodyAsync(HttpClient client, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/quotes", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(raw);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Invalid request.", problem.RootElement.GetProperty("title").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("JsonException", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BadHttpRequestException", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Text.Json", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stone", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Teleport", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BytePosition", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", raw, StringComparison.OrdinalIgnoreCase);
    }
    private static string RequestJson(string package, string suffix = "") =>
        $$"""{"origin":{"countryCode":"EG","city":"Cairo"},"destination":{"countryCode":"US","city":"New York"},"packages":[{{package}}],"currency":"USD"{{suffix}}}""";
    private static JsonSerializerOptions Options() { var value = new JsonSerializerOptions(JsonSerializerDefaults.Web); value.Converters.Add(new JsonStringEnumConverter()); return value; }
}
