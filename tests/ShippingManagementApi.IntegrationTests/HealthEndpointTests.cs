using System.Net;

namespace ShippingManagementApi.IntegrationTests;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task GetHealth_StartsApplicationAndReturnsSuccess()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task GetOpenApi_ReturnsDocumentWithHealthEndpoint()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ShippingManagementApi", document, StringComparison.Ordinal);
        Assert.Contains("/health", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetScalar_ReturnsInteractiveApiReference()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
