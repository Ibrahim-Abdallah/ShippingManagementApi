using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingManagementApi.Domain.Merchants;
using ShippingManagementApi.Infrastructure.Identity;
using ShippingManagementApi.Domain.Carriers;

namespace ShippingManagementApi.Infrastructure.Persistence;

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("Merchants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(Merchant.MaximumNameLength).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(Merchant.MaximumCodeLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.IsActive).IsRequired();
        builder.HasOne(x => x.Merchant).WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.MerchantId);
        builder.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReplacedByToken).WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal static class DemoCarrierSeed
{
    public static readonly Guid CarrierId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid StandardServiceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid ExpressServiceId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly DateTimeOffset CreatedAtUtc = new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> builder)
    {
        builder.ToTable("Carriers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(Carrier.MaximumCodeLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Carrier.MaximumNameLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasMany(x => x.Services).WithOne(x => x.Carrier).HasForeignKey(x => x.CarrierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new
        {
            Id = DemoCarrierSeed.CarrierId, Code = "DEMO", Name = "Demo Carrier", IsActive = true,
            SupportsPickup = true, SupportsTracking = true, SupportsCancellation = true, SupportsCod = true,
            CreatedAtUtc = DemoCarrierSeed.CreatedAtUtc, UpdatedAtUtc = DemoCarrierSeed.CreatedAtUtc
        });
    }
}

internal sealed class CarrierServiceConfiguration : IEntityTypeConfiguration<CarrierService>
{
    public void Configure(EntityTypeBuilder<CarrierService> builder)
    {
        builder.ToTable("CarrierServices", table =>
        {
            table.HasCheckConstraint("CK_CarrierServices_EstimatedMinDays", "[EstimatedMinDays] >= 0");
            table.HasCheckConstraint("CK_CarrierServices_EstimatedRange", "[EstimatedMaxDays] >= [EstimatedMinDays]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(CarrierService.MaximumCodeLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(CarrierService.MaximumNameLength).IsRequired();
        builder.Property(x => x.ServiceLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.CarrierId, x.Code }).IsUnique();
        builder.HasIndex(x => x.CarrierId);
        builder.HasIndex(x => x.IsActive);
        builder.HasData(
            new
            {
                Id = DemoCarrierSeed.StandardServiceId, CarrierId = DemoCarrierSeed.CarrierId, Code = "STANDARD", Name = "Demo Standard",
                ServiceLevel = ServiceLevel.Standard, IsActive = true, EstimatedMinDays = 2, EstimatedMaxDays = 5,
                CreatedAtUtc = DemoCarrierSeed.CreatedAtUtc, UpdatedAtUtc = DemoCarrierSeed.CreatedAtUtc
            },
            new
            {
                Id = DemoCarrierSeed.ExpressServiceId, CarrierId = DemoCarrierSeed.CarrierId, Code = "EXPRESS", Name = "Demo Express",
                ServiceLevel = ServiceLevel.Express, IsActive = true, EstimatedMinDays = 1, EstimatedMaxDays = 2,
                CreatedAtUtc = DemoCarrierSeed.CreatedAtUtc, UpdatedAtUtc = DemoCarrierSeed.CreatedAtUtc
            });
    }
}
