using System.ComponentModel.DataAnnotations;
using ShippingManagementApi.Application.Quotes;
using ShippingManagementApi.Application.Security;

namespace ShippingManagementApi.Api.Endpoints;

internal static class Phase04Endpoints
{
    public static IEndpointRouteBuilder MapPhase04Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var quotes = endpoints.MapGroup("/api/quotes").RequireAuthorization(AuthorizationPolicies.MerchantOnly).WithTags("Shipping Quotes");
        quotes.MapPost("/", Create).WithSummary("Rates eligible carrier services and creates an immutable shipping quote");
        quotes.MapGet("/{quoteId:guid}", Get).WithSummary("Returns a merchant-owned shipping quote");
        quotes.MapGet("/", List).WithSummary("Lists merchant-owned quote history, newest first");
        return endpoints;
    }
    private static async Task<IResult> Create(CreateShippingQuoteRequest request, IShippingQuoteService service, CancellationToken ct)
    {
        var validation = Validate(request); if (validation is not null) return validation;
        var result = await service.CreateAsync(request, ct);
        return result.IsSuccess ? Results.Created($"/api/quotes/{result.Value!.QuoteId}", result.Value) : Problem(result);
    }
    private static async Task<IResult> Get(Guid quoteId, IShippingQuoteService service, CancellationToken ct) => Result(await service.GetAsync(quoteId, ct));
    private static async Task<IResult> List(IShippingQuoteService service, CancellationToken ct, int page = 1, int pageSize = 20) =>
        Result(await service.ListAsync(page, pageSize, ct));
    private static IResult Result<T>(ServiceResult<T> result) => result.IsSuccess ? Results.Ok(result.Value) : Problem(result);
    private static IResult Problem<T>(ServiceResult<T> result) => result.Error switch
    {
        ServiceError.NotFound => Results.Problem(statusCode: 404, title: "Not found.", detail: result.Detail),
        ServiceError.Conflict => Results.Problem(statusCode: 409, title: "Conflict.", detail: result.Detail),
        ServiceError.Forbidden => Results.Problem(statusCode: 403, title: "Access is forbidden.", detail: result.Detail),
        _ => Results.Problem(statusCode: 400, title: "Validation failed.", detail: result.Detail)
    };
    private static IResult? Validate<T>(T request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request!, new ValidationContext(request!), results, true)) return null;
        return Results.ValidationProblem(results.GroupBy(x => x.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage ?? "Invalid value.").ToArray()));
    }
}
