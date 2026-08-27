using Microsoft.AspNetCore.Identity;
using ShippingManagementApi.Domain.Merchants;

namespace ShippingManagementApi.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; } = [];
}
