using System.ComponentModel.DataAnnotations;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.Api.Endpoints;

internal static class Phase03Endpoints
{
    public static IEndpointRouteBuilder MapPhase03Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var carriers = endpoints.MapGroup("/api/admin/carriers").RequireAuthorization(AuthorizationPolicies.AdminOnly).WithTags("Carriers");
        carriers.MapPost("/", CreateCarrier).WithSummary("Creates a carrier configuration");
        carriers.MapGet("/", ListCarriers).WithSummary("Lists all carrier configuration");
        carriers.MapGet("/{carrierId:guid}", GetCarrier).WithSummary("Returns carrier configuration");
        carriers.MapPut("/{carrierId:guid}", UpdateCarrier).WithSummary("Updates carrier details and capabilities");
        carriers.MapDelete("/{carrierId:guid}", DeleteCarrier).WithSummary("Deletes a carrier only when it has no services");
        carriers.MapPatch("/{carrierId:guid}/activation", SetCarrierActivation).WithSummary("Activates or deactivates a carrier");

        var services = endpoints.MapGroup("/api/admin/carriers/{carrierId:guid}/services")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly).WithTags("Carrier Services");
        services.MapPost("/", CreateService).WithSummary("Creates a service under a carrier");
        services.MapGet("/", ListServices).WithSummary("Lists all services under a carrier");
        services.MapGet("/{serviceId:guid}", GetService).WithSummary("Returns a carrier-owned service");
        services.MapPut("/{serviceId:guid}", UpdateService).WithSummary("Updates a carrier-owned service");
        services.MapDelete("/{serviceId:guid}", DeleteService).WithSummary("Deletes a carrier-owned service");
        services.MapPatch("/{serviceId:guid}/activation", SetServiceActivation).WithSummary("Activates or deactivates a carrier-owned service");

        var catalog = endpoints.MapGroup("/api/carriers").RequireAuthorization().WithTags("Carrier Catalog");
        catalog.MapGet("/", ListCatalog).WithSummary("Lists active carriers and active services");
        catalog.MapGet("/{carrierId:guid}/services", ListCatalogServices).WithSummary("Lists active services for an active carrier");
        return endpoints;
    }

    private static async Task<IResult> CreateCarrier(CreateCarrierRequest request, ICarrierManagementService service, CancellationToken ct)
    {
        var validation = Validate(request); if (validation is not null) return validation;
        var result = await service.CreateAsync(request, ct);
        return result.IsSuccess ? Results.Created($"/api/admin/carriers/{result.Value!.Id}", result.Value) : ToProblem(result.Error, result.Detail);
    }
    private static async Task<IResult> ListCarriers(ICarrierManagementService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct));
    private static async Task<IResult> GetCarrier(Guid carrierId, ICarrierManagementService service, CancellationToken ct) => ToResult(await service.GetAsync(carrierId, ct));
    private static async Task<IResult> UpdateCarrier(Guid carrierId, UpdateCarrierRequest request, ICarrierManagementService service, CancellationToken ct)
    { var validation = Validate(request); return validation ?? ToResult(await service.UpdateAsync(carrierId, request, ct)); }
    private static async Task<IResult> DeleteCarrier(Guid carrierId, ICarrierManagementService service, CancellationToken ct)
    { var result = await service.DeleteAsync(carrierId, ct); return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, result.Detail); }
    private static async Task<IResult> SetCarrierActivation(Guid carrierId, SetActivationRequest request, ICarrierManagementService service, CancellationToken ct)
    {
        var validation = Validate(request);
        return validation ?? ToResult(await service.SetActivationAsync(carrierId, request.IsActive!.Value, ct));
    }

    private static async Task<IResult> CreateService(Guid carrierId, CreateCarrierServiceRequest request, ICarrierManagementService service, CancellationToken ct)
    {
        var validation = Validate(request); if (validation is not null) return validation;
        var result = await service.CreateServiceAsync(carrierId, request, ct);
        return result.IsSuccess ? Results.Created($"/api/admin/carriers/{carrierId}/services/{result.Value!.Id}", result.Value) : ToProblem(result.Error, result.Detail);
    }
    private static async Task<IResult> ListServices(Guid carrierId, ICarrierManagementService service, CancellationToken ct) => ToResult(await service.ListServicesAsync(carrierId, ct));
    private static async Task<IResult> GetService(Guid carrierId, Guid serviceId, ICarrierManagementService service, CancellationToken ct) => ToResult(await service.GetServiceAsync(carrierId, serviceId, ct));
    private static async Task<IResult> UpdateService(Guid carrierId, Guid serviceId, UpdateCarrierServiceRequest request, ICarrierManagementService service, CancellationToken ct)
    { var validation = Validate(request); return validation ?? ToResult(await service.UpdateServiceAsync(carrierId, serviceId, request, ct)); }
    private static async Task<IResult> DeleteService(Guid carrierId, Guid serviceId, ICarrierManagementService service, CancellationToken ct)
    { var result = await service.DeleteServiceAsync(carrierId, serviceId, ct); return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, result.Detail); }
    private static async Task<IResult> SetServiceActivation(Guid carrierId, Guid serviceId, SetActivationRequest request, ICarrierManagementService service, CancellationToken ct)
    {
        var validation = Validate(request);
        return validation ?? ToResult(await service.SetServiceActivationAsync(carrierId, serviceId, request.IsActive!.Value, ct));
    }

    private static async Task<IResult> ListCatalog(ICarrierCatalogService service, CancellationToken ct) => Results.Ok(await service.ListAvailableAsync(ct));
    private static async Task<IResult> ListCatalogServices(Guid carrierId, ICarrierCatalogService service, CancellationToken ct) => ToResult(await service.ListAvailableServicesAsync(carrierId, ct));

    private static IResult ToResult<T>(ServiceResult<T> result) => result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error, result.Detail);
    private static IResult ToProblem(ServiceError error, string? detail) => error switch
    {
        ServiceError.NotFound => Results.Problem(statusCode: 404, title: "Not found.", detail: detail),
        ServiceError.Conflict => Results.Problem(statusCode: 409, title: "Conflict.", detail: detail),
        _ => Results.Problem(statusCode: 400, title: "Validation failed.", detail: detail)
    };
    private static IResult? Validate<T>(T request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request!, new ValidationContext(request!), results, true)) return null;
        return Results.ValidationProblem(results.GroupBy(x => x.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage ?? "Invalid value.").ToArray()));
    }
}
