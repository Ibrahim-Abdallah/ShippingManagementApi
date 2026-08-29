using System.ComponentModel.DataAnnotations;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Carriers;

namespace ShippingManagementApi.Application.Carriers;

public sealed record CarrierCapabilities(bool SupportsPickup, bool SupportsTracking, bool SupportsCancellation, bool SupportsCod);
public sealed record CarrierResponse(Guid Id, string Code, string Name, bool IsActive, CarrierCapabilities Capabilities,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record CarrierServiceResponse(Guid Id, Guid CarrierId, string Code, string Name, ServiceLevel ServiceLevel,
    bool IsActive, int EstimatedMinDays, int EstimatedMaxDays);
public sealed record CarrierCatalogResponse(Guid Id, string Code, string Name, CarrierCapabilities Capabilities,
    IReadOnlyCollection<CarrierServiceResponse> Services);

public sealed record CreateCarrierRequest(
    [property: Required, MaxLength(Carrier.MaximumCodeLength)] string Code,
    [property: Required, MaxLength(Carrier.MaximumNameLength)] string Name,
    bool SupportsPickup, bool SupportsTracking, bool SupportsCancellation, bool SupportsCod);
public sealed record UpdateCarrierRequest(
    [property: Required, MaxLength(Carrier.MaximumNameLength)] string Name,
    bool SupportsPickup, bool SupportsTracking, bool SupportsCancellation, bool SupportsCod);
public sealed record SetActivationRequest([property: Required] bool? IsActive);
public sealed record CreateCarrierServiceRequest(
    [property: Required, MaxLength(CarrierService.MaximumCodeLength)] string Code,
    [property: Required, MaxLength(CarrierService.MaximumNameLength)] string Name,
    [property: Required] ServiceLevel? ServiceLevel,
    [property: Required] int? EstimatedMinDays,
    [property: Required] int? EstimatedMaxDays);
public sealed record UpdateCarrierServiceRequest(
    [property: Required, MaxLength(CarrierService.MaximumNameLength)] string Name,
    [property: Required] ServiceLevel? ServiceLevel,
    [property: Required] int? EstimatedMinDays,
    [property: Required] int? EstimatedMaxDays);

public interface ICarrierManagementService
{
    Task<ServiceResult<CarrierResponse>> CreateAsync(CreateCarrierRequest request, CancellationToken ct);
    Task<IReadOnlyCollection<CarrierResponse>> ListAsync(CancellationToken ct);
    Task<ServiceResult<CarrierResponse>> GetAsync(Guid carrierId, CancellationToken ct);
    Task<ServiceResult<CarrierResponse>> UpdateAsync(Guid carrierId, UpdateCarrierRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(Guid carrierId, CancellationToken ct);
    Task<ServiceResult<CarrierResponse>> SetActivationAsync(Guid carrierId, bool isActive, CancellationToken ct);
    Task<ServiceResult<CarrierServiceResponse>> CreateServiceAsync(Guid carrierId, CreateCarrierServiceRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>> ListServicesAsync(Guid carrierId, CancellationToken ct);
    Task<ServiceResult<CarrierServiceResponse>> GetServiceAsync(Guid carrierId, Guid serviceId, CancellationToken ct);
    Task<ServiceResult<CarrierServiceResponse>> UpdateServiceAsync(Guid carrierId, Guid serviceId, UpdateCarrierServiceRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteServiceAsync(Guid carrierId, Guid serviceId, CancellationToken ct);
    Task<ServiceResult<CarrierServiceResponse>> SetServiceActivationAsync(Guid carrierId, Guid serviceId, bool isActive, CancellationToken ct);
}

public interface ICarrierCatalogService
{
    Task<IReadOnlyCollection<CarrierCatalogResponse>> ListAvailableAsync(CancellationToken ct);
    Task<ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>> ListAvailableServicesAsync(Guid carrierId, CancellationToken ct);
}

public sealed record CarrierProviderCapabilities(bool SupportsPickup, bool SupportsTracking, bool SupportsCancellation, bool SupportsCod);
public interface ICarrierProvider
{
    string CarrierCode { get; }
    CarrierProviderCapabilities Capabilities { get; }
}
public interface ICarrierProviderResolver
{
    ICarrierProvider Resolve(string carrierCode);
    bool TryResolve(string carrierCode, out ICarrierProvider? provider);
}
