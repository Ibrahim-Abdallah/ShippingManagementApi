using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ShippingManagementApi.Application.Merchants;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.Api.Endpoints;

internal static class Phase02Endpoints
{
    public static IEndpointRouteBuilder MapPhase02Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Authentication");
        auth.MapPost("/login", Login).AllowAnonymous().WithSummary("Authenticates a user and issues a token pair");
        auth.MapPost("/refresh", Refresh).AllowAnonymous().WithSummary("Rotates a refresh token and issues a new token pair");
        auth.MapPost("/logout", Logout).AllowAnonymous().WithSummary("Revokes a refresh token");
        auth.MapGet("/me", Me).RequireAuthorization().WithSummary("Returns the authenticated user");

        endpoints.MapPost("/api/admin/merchants", ProvisionMerchant)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly).WithTags("Merchants")
            .WithSummary("Provisions a merchant and its initial user");
        endpoints.MapGet("/api/merchants/{id:guid}", GetMerchant)
            .RequireAuthorization().WithTags("Merchants")
            .WithSummary("Returns a merchant subject to trusted tenant scope");
        return endpoints;
    }

    private static async Task<IResult> Login(LoginRequest request, IAuthenticationService service, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        return ToResult(await service.LoginAsync(request, ct), StatusCodes.Status200OK);
    }

    private static async Task<IResult> Refresh(RefreshRequest request, IAuthenticationService service, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        return ToResult(await service.RefreshAsync(request.RefreshToken, ct), StatusCodes.Status200OK);
    }

    private static async Task<IResult> Logout(LogoutRequest request, IAuthenticationService service, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        var result = await service.LogoutAsync(request.RefreshToken, ct);
        return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, result.Detail);
    }

    private static async Task<IResult> Me(IAuthenticationService service, CancellationToken ct) =>
        ToResult(await service.GetCurrentUserAsync(ct), StatusCodes.Status200OK);

    private static async Task<IResult> ProvisionMerchant(ProvisionMerchantRequest request, IMerchantService service, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        return ToResult(await service.ProvisionAsync(request, ct), StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetMerchant(Guid id, IMerchantService service, CancellationToken ct) =>
        ToResult(await service.GetAsync(id, ct), StatusCodes.Status200OK);

    private static IResult ToResult<T>(ServiceResult<T> result, int statusCode) => result.IsSuccess
        ? Results.Json(result.Value, statusCode: statusCode)
        : ToProblem(result.Error, result.Detail);

    private static IResult ToProblem(ServiceError error, string? detail) => error switch
    {
        ServiceError.InvalidCredentials or ServiceError.InvalidToken => Results.Problem(statusCode: 401, title: "Authentication failed.", detail: detail),
        ServiceError.Forbidden => Results.Problem(statusCode: 403, title: "Forbidden.", detail: detail),
        ServiceError.NotFound => Results.Problem(statusCode: 404, title: "Not found.", detail: detail),
        ServiceError.Conflict => Results.Problem(statusCode: 409, title: "Conflict.", detail: detail),
        _ => Results.Problem(statusCode: 400, title: "Validation failed.", detail: detail)
    };

    private static IResult? Validate<T>(T request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request!, new ValidationContext(request!), results, true)) return null;
        var errors = results.GroupBy(x => x.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage ?? "Invalid value.").ToArray());
        return Results.ValidationProblem(errors);
    }
}
