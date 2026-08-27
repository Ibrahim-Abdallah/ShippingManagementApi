using System.ComponentModel.DataAnnotations;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.Application.Merchants;

public sealed record ProvisionMerchantRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(50)] string Code,
    [property: Required, EmailAddress, MaxLength(256)] string InitialUserEmail,
    [property: Required] string InitialUserPassword);

public sealed record MerchantResponse(Guid Id, string Name, string Code, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public interface IMerchantService
{
    Task<ServiceResult<MerchantResponse>> ProvisionAsync(ProvisionMerchantRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<MerchantResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
}
