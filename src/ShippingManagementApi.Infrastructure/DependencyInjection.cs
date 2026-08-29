using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ShippingManagementApi.Application.Merchants;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Infrastructure.Identity;
using ShippingManagementApi.Infrastructure.Merchants;
using ShippingManagementApi.Infrastructure.Persistence;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Infrastructure.Carriers;

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
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ShippingManagementDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be configured externally and contain at least 32 UTF-8 bytes.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                    ValidateAudience = true, ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub", RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy(AuthorizationPolicies.OperatorOrAdmin, policy => policy.RequireRole(AppRoles.Operator, AppRoles.Admin))
            .AddPolicy(AuthorizationPolicies.MerchantOnly, policy => policy.RequireRole(AppRoles.Merchant));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IMerchantService, MerchantService>();
        services.AddScoped<ICarrierManagementService, CarrierManagementService>();
        services.AddScoped<ICarrierCatalogService, CarrierCatalogService>();
        services.AddSingleton<ICarrierProvider, DemoCarrier>();
        services.AddSingleton<ICarrierProviderResolver, CarrierProviderResolver>();
        return services;
    }
}
