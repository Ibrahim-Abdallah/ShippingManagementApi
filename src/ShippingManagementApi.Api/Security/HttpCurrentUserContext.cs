using System.Security.Claims;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.Api.Security;

internal sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public Guid? UserId => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid? MerchantId => Guid.TryParse(User.FindFirstValue("merchant_id"), out var id) ? id : null;
    public IReadOnlyCollection<string> Roles => User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
}
