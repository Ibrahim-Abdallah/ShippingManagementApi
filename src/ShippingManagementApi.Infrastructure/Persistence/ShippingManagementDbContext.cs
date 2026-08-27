using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShippingManagementApi.Domain.Merchants;
using ShippingManagementApi.Infrastructure.Identity;

namespace ShippingManagementApi.Infrastructure.Persistence;

public sealed class ShippingManagementDbContext(DbContextOptions<ShippingManagementDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShippingManagementDbContext).Assembly);
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid> { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "11111111-1111-1111-1111-111111111111" },
            new IdentityRole<Guid> { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Operator", NormalizedName = "OPERATOR", ConcurrencyStamp = "22222222-2222-2222-2222-222222222222" },
            new IdentityRole<Guid> { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Merchant", NormalizedName = "MERCHANT", ConcurrencyStamp = "33333333-3333-3333-3333-333333333333" });
    }
}
