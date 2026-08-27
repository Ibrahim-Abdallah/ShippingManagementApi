using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ShippingManagementApi.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ShippingManagementDb",
            "Server=(localdb)\\mssqllocaldb;Database=ShippingManagementDbTests;Trusted_Connection=True;TrustServerCertificate=True");
    }
}
