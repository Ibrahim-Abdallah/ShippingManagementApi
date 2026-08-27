using Microsoft.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using ShippingManagementApi.Api.Diagnostics;
using ShippingManagementApi.Api.Middleware;
using ShippingManagementApi.Application;
using ShippingManagementApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

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

        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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

app.Run();

public partial class Program;
