using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.Infrastructure.Identity;

public static class DevelopmentAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["DevelopmentSeed:AdminEmail"];
        var password = configuration["DevelopmentSeed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Development administrator seeding requires DevelopmentSeed:AdminEmail and DevelopmentSeed:AdminPassword.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is null)
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
            existing = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var result = await users.CreateAsync(existing, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Development administrator could not be created: " +
                    string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (!await users.IsInRoleAsync(existing, AppRoles.Admin))
        {
            var result = await users.AddToRoleAsync(existing, AppRoles.Admin);
            if (!result.Succeeded) throw new InvalidOperationException("Development administrator role assignment failed.");
        }
    }
}
