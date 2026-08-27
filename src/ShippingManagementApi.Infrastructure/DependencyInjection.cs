using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "ShippingManagementDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<ShippingManagementDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
