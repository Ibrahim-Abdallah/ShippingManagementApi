using Microsoft.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using ShippingManagementApi.Api.Diagnostics;
using ShippingManagementApi.Api.Endpoints;
using ShippingManagementApi.Api.Middleware;
using ShippingManagementApi.Api.Security;
using ShippingManagementApi.Application;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Infrastructure;
using ShippingManagementApi.Infrastructure.Identity;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "ShippingManagementApi",
            Version = "v1",
            Description = "Shipping management and carrier orchestration API."
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Supply the JWT access token issued by /api/auth/login."
        };
        var bearerRequirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        };
        foreach (var path in document.Paths.Keys.Where(path =>
                     path.StartsWith("/api/admin/", StringComparison.Ordinal) ||
                     path.StartsWith("/api/carriers", StringComparison.Ordinal) ||
                     path.StartsWith("/api/quotes", StringComparison.Ordinal) ||
                     path is "/api/auth/me" or "/api/merchants/{id}"))
        {
            if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem.Operations is null) continue;
            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(bearerRequirement);
            }
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var service = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await service.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = context.HttpContext,
        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = context.HttpContext.Response.StatusCode,
            Title = context.HttpContext.Response.StatusCode switch
            {
                401 => "Authentication is required.",
                403 => "Access is forbidden.",
                404 => "Resource not found.",
                _ => "The request could not be completed."
            }
        }
    });
});
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPhase02Endpoints();
app.MapPhase03Endpoints();
app.MapPhase04Endpoints();

app.MapGet("/health", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        return report.Status == HealthStatus.Healthy
            ? Results.Ok(new { status = report.Status.ToString() })
            : Results.Json(
                new { status = report.Status.ToString() },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .WithName("Health")
    .WithSummary("Reports application health")
    .WithDescription("Returns success when the API process is healthy.")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentAdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.Run();

public partial class Program;
