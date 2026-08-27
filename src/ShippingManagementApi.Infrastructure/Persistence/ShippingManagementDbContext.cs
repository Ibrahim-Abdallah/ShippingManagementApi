using Microsoft.EntityFrameworkCore;

namespace ShippingManagementApi.Infrastructure.Persistence;

public sealed class ShippingManagementDbContext(DbContextOptions<ShippingManagementDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShippingManagementDbContext).Assembly);
    }
}
