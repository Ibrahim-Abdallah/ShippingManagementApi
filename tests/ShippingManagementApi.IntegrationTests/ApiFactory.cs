using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Merchants;
using ShippingManagementApi.Infrastructure.Identity;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ShippingManagementDb", "unused");
        builder.UseSetting("Jwt:Issuer", "ShippingManagementApi.Tests");
        builder.UseSetting("Jwt:Audience", "ShippingManagementApi.Tests.Client");
        builder.UseSetting("Jwt:SigningKey", "TEST-ONLY-signing-key-with-at-least-32-bytes-0001");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ShippingManagementDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ShippingManagementDbContext>>();
            services.AddDbContext<ShippingManagementDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>().Database.EnsureCreated();
        return host;
    }

    public async Task<(Guid UserId, Guid? MerchantId)> CreateUserAsync(string email, string password, string role,
        string? merchantCode = null, bool userActive = true, bool merchantActive = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
        Merchant? merchant = null;
        if (role == AppRoles.Merchant)
        {
            merchant = new Merchant("Test " + merchantCode, merchantCode!, DateTimeOffset.UtcNow);
            db.Merchants.Add(merchant);
            await db.SaveChangesAsync();
            if (!merchantActive)
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Merchants SET IsActive = 0 WHERE Id = {merchant.Id}");
        }
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true, IsActive = userActive,
            MerchantId = merchant?.Id, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        Assert.True((await manager.CreateAsync(user, password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return (user.Id, merchant?.Id);
    }

    public async Task ExpireRefreshTokenAsync(string rawToken)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
        var hash = RefreshTokenSecurity.Hash(rawToken);
        await db.RefreshTokens.Where(x => x.TokenHash == hash)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    public async Task<bool> RawRefreshTokenIsPersistedAsync(string rawToken)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShippingManagementDbContext>();
        return await db.RefreshTokens.AnyAsync(x => x.TokenHash == rawToken);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
