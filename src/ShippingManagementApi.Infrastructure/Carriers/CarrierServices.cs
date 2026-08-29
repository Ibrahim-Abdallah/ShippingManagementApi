using Microsoft.EntityFrameworkCore;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Carriers;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.Infrastructure.Carriers;

internal sealed class CarrierManagementService(ShippingManagementDbContext dbContext, TimeProvider timeProvider)
    : ICarrierManagementService
{
    public async Task<ServiceResult<CarrierResponse>> CreateAsync(CreateCarrierRequest request, CancellationToken ct)
    {
        Carrier carrier;
        try { carrier = new Carrier(request.Code, request.Name, request.SupportsPickup, request.SupportsTracking, request.SupportsCancellation, request.SupportsCod, timeProvider.GetUtcNow()); }
        catch (ArgumentException ex) { return Invalid<CarrierResponse>(ex); }
        if (await dbContext.Carriers.AnyAsync(x => x.Code == carrier.Code, ct)) return Conflict<CarrierResponse>("A carrier with this code already exists.");
        dbContext.Carriers.Add(carrier);
        if (!await SaveAsync(ct)) return Conflict<CarrierResponse>("A carrier with this code already exists.");
        return ServiceResult<CarrierResponse>.Success(Map(carrier));
    }

    public async Task<IReadOnlyCollection<CarrierResponse>> ListAsync(CancellationToken ct) =>
        await dbContext.Carriers.AsNoTracking().OrderBy(x => x.Code).Select(x => Map(x)).ToArrayAsync(ct);

    public async Task<ServiceResult<CarrierResponse>> GetAsync(Guid carrierId, CancellationToken ct)
    {
        var carrier = await dbContext.Carriers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == carrierId, ct);
        return carrier is null ? NotFound<CarrierResponse>("Carrier was not found.") : ServiceResult<CarrierResponse>.Success(Map(carrier));
    }

    public async Task<ServiceResult<CarrierResponse>> UpdateAsync(Guid carrierId, UpdateCarrierRequest request, CancellationToken ct)
    {
        var carrier = await dbContext.Carriers.SingleOrDefaultAsync(x => x.Id == carrierId, ct);
        if (carrier is null) return NotFound<CarrierResponse>("Carrier was not found.");
        try { carrier.Update(request.Name, request.SupportsPickup, request.SupportsTracking, request.SupportsCancellation, request.SupportsCod, timeProvider.GetUtcNow()); }
        catch (ArgumentException ex) { return Invalid<CarrierResponse>(ex); }
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<CarrierResponse>.Success(Map(carrier));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid carrierId, CancellationToken ct)
    {
        var carrier = await dbContext.Carriers.SingleOrDefaultAsync(x => x.Id == carrierId, ct);
        if (carrier is null) return NotFound<bool>("Carrier was not found.");
        if (await dbContext.CarrierServices.AnyAsync(x => x.CarrierId == carrierId, ct)) return Conflict<bool>("A carrier with services cannot be deleted. Deactivate it instead.");
        dbContext.Carriers.Remove(carrier);
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<CarrierResponse>> SetActivationAsync(Guid carrierId, bool isActive, CancellationToken ct)
    {
        var carrier = await dbContext.Carriers.SingleOrDefaultAsync(x => x.Id == carrierId, ct);
        if (carrier is null) return NotFound<CarrierResponse>("Carrier was not found.");
        carrier.SetActivation(isActive, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<CarrierResponse>.Success(Map(carrier));
    }

    public async Task<ServiceResult<CarrierServiceResponse>> CreateServiceAsync(Guid carrierId, CreateCarrierServiceRequest request, CancellationToken ct)
    {
        if (!await dbContext.Carriers.AnyAsync(x => x.Id == carrierId, ct)) return NotFound<CarrierServiceResponse>("Carrier was not found.");
        CarrierService service;
        try { service = new CarrierService(carrierId, request.Code, request.Name, request.ServiceLevel!.Value, request.EstimatedMinDays!.Value, request.EstimatedMaxDays!.Value, timeProvider.GetUtcNow()); }
        catch (ArgumentException ex) { return Invalid<CarrierServiceResponse>(ex); }
        if (await dbContext.CarrierServices.AnyAsync(x => x.CarrierId == carrierId && x.Code == service.Code, ct)) return Conflict<CarrierServiceResponse>("A service with this code already exists for the carrier.");
        dbContext.CarrierServices.Add(service);
        if (!await SaveAsync(ct)) return Conflict<CarrierServiceResponse>("A service with this code already exists for the carrier.");
        return ServiceResult<CarrierServiceResponse>.Success(Map(service));
    }

    public async Task<ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>> ListServicesAsync(Guid carrierId, CancellationToken ct)
    {
        if (!await dbContext.Carriers.AnyAsync(x => x.Id == carrierId, ct)) return NotFound<IReadOnlyCollection<CarrierServiceResponse>>("Carrier was not found.");
        var services = await dbContext.CarrierServices.AsNoTracking().Where(x => x.CarrierId == carrierId).OrderBy(x => x.Code).Select(x => Map(x)).ToArrayAsync(ct);
        return ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>.Success(services);
    }

    public async Task<ServiceResult<CarrierServiceResponse>> GetServiceAsync(Guid carrierId, Guid serviceId, CancellationToken ct)
    {
        var service = await dbContext.CarrierServices.AsNoTracking().SingleOrDefaultAsync(x => x.CarrierId == carrierId && x.Id == serviceId, ct);
        return service is null ? NotFound<CarrierServiceResponse>("Carrier service was not found.") : ServiceResult<CarrierServiceResponse>.Success(Map(service));
    }

    public async Task<ServiceResult<CarrierServiceResponse>> UpdateServiceAsync(Guid carrierId, Guid serviceId, UpdateCarrierServiceRequest request, CancellationToken ct)
    {
        var service = await dbContext.CarrierServices.SingleOrDefaultAsync(x => x.CarrierId == carrierId && x.Id == serviceId, ct);
        if (service is null) return NotFound<CarrierServiceResponse>("Carrier service was not found.");
        try { service.Update(request.Name, request.ServiceLevel!.Value, request.EstimatedMinDays!.Value, request.EstimatedMaxDays!.Value, timeProvider.GetUtcNow()); }
        catch (ArgumentException ex) { return Invalid<CarrierServiceResponse>(ex); }
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<CarrierServiceResponse>.Success(Map(service));
    }

    public async Task<ServiceResult<bool>> DeleteServiceAsync(Guid carrierId, Guid serviceId, CancellationToken ct)
    {
        var service = await dbContext.CarrierServices.SingleOrDefaultAsync(x => x.CarrierId == carrierId && x.Id == serviceId, ct);
        if (service is null) return NotFound<bool>("Carrier service was not found.");
        dbContext.CarrierServices.Remove(service);
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<CarrierServiceResponse>> SetServiceActivationAsync(Guid carrierId, Guid serviceId, bool isActive, CancellationToken ct)
    {
        var service = await dbContext.CarrierServices.SingleOrDefaultAsync(x => x.CarrierId == carrierId && x.Id == serviceId, ct);
        if (service is null) return NotFound<CarrierServiceResponse>("Carrier service was not found.");
        service.SetActivation(isActive, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<CarrierServiceResponse>.Success(Map(service));
    }

    private async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex) when (SqlServerDatabaseErrorClassifier.IsUniqueConstraintViolation(ex)) { return false; }
    }
    private static ServiceResult<T> Invalid<T>(ArgumentException ex) => ServiceResult<T>.Fail(ServiceError.Validation, ex.Message);
    private static ServiceResult<T> Conflict<T>(string detail) => ServiceResult<T>.Fail(ServiceError.Conflict, detail);
    private static ServiceResult<T> NotFound<T>(string detail) => ServiceResult<T>.Fail(ServiceError.NotFound, detail);
    private static CarrierResponse Map(Carrier x) => new(x.Id, x.Code, x.Name, x.IsActive,
        new(x.SupportsPickup, x.SupportsTracking, x.SupportsCancellation, x.SupportsCod), x.CreatedAtUtc, x.UpdatedAtUtc);
    private static CarrierServiceResponse Map(CarrierService x) => new(x.Id, x.CarrierId, x.Code, x.Name, x.ServiceLevel, x.IsActive, x.EstimatedMinDays, x.EstimatedMaxDays);
}

internal sealed class CarrierCatalogService(ShippingManagementDbContext dbContext) : ICarrierCatalogService
{
    public async Task<IReadOnlyCollection<CarrierCatalogResponse>> ListAvailableAsync(CancellationToken ct)
    {
        var carriers = await dbContext.Carriers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new CarrierCatalogResponse(x.Id, x.Code, x.Name,
                new(x.SupportsPickup, x.SupportsTracking, x.SupportsCancellation, x.SupportsCod),
                x.Services.Where(s => s.IsActive).OrderBy(s => s.Code)
                    .Select(s => new CarrierServiceResponse(s.Id, s.CarrierId, s.Code, s.Name, s.ServiceLevel, s.IsActive, s.EstimatedMinDays, s.EstimatedMaxDays)).ToArray()))
            .ToArrayAsync(ct);
        return carriers;
    }

    public async Task<ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>> ListAvailableServicesAsync(Guid carrierId, CancellationToken ct)
    {
        if (!await dbContext.Carriers.AsNoTracking().AnyAsync(x => x.Id == carrierId && x.IsActive, ct))
            return ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>.Fail(ServiceError.NotFound, "Active carrier was not found.");
        var services = await dbContext.CarrierServices.AsNoTracking().Where(x => x.CarrierId == carrierId && x.IsActive).OrderBy(x => x.Code)
            .Select(x => new CarrierServiceResponse(x.Id, x.CarrierId, x.Code, x.Name, x.ServiceLevel, x.IsActive, x.EstimatedMinDays, x.EstimatedMaxDays)).ToArrayAsync(ct);
        return ServiceResult<IReadOnlyCollection<CarrierServiceResponse>>.Success(services);
    }
}
