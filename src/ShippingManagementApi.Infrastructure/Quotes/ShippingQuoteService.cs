using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShippingManagementApi.Application.Carriers;
using ShippingManagementApi.Application.Quotes;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Quotes;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.Infrastructure.Quotes;

internal sealed class ShippingQuoteService(ShippingManagementDbContext dbContext, ICurrentUserContext currentUser,
    ICarrierProviderResolver providerResolver, TimeProvider timeProvider, IOptions<ShippingQuoteOptions> options) : IShippingQuoteService
{
    public async Task<ServiceResult<ShippingQuoteResponse>> CreateAsync(CreateShippingQuoteRequest request, CancellationToken ct)
    {
        if (currentUser.MerchantId is not { } merchantId) return Fail<ShippingQuoteResponse>(ServiceError.Forbidden, "Merchant context is required.");
        QuoteAddress origin, destination; string currency; List<ShippingQuotePackage> packages;
        try
        {
            origin = new(request.Origin!.CountryCode!, request.Origin.City!, request.Origin.StateOrProvince, request.Origin.PostalCode, request.Origin.AddressLine1);
            destination = new(request.Destination!.CountryCode!, request.Destination.City!, request.Destination.StateOrProvince, request.Destination.PostalCode, request.Destination.AddressLine1);
            currency = ShippingQuote.NormalizeCurrency(request.Currency!);
            packages = request.Packages!.Select(x => new ShippingQuotePackage(x.Weight!.Value, x.WeightUnit!.Value,
                x.Length, x.Width, x.Height, x.DimensionUnit, x.DeclaredValue)).ToList();
        }
        catch (ArgumentException ex) { return Fail<ShippingQuoteResponse>(ServiceError.Validation, ex.Message); }

        var levels = request.RequestedServiceLevels?.ToHashSet();
        var carriers = await dbContext.Carriers.AsNoTracking().Where(x => x.IsActive && (!request.RequiresCod || x.SupportsCod))
            .Select(x => new
            {
                x.Id, x.Code, x.Name,
                Services = x.Services.Where(s => s.IsActive && (levels == null || levels.Contains(s.ServiceLevel)))
                    .Select(s => new { s.Id, s.Code, s.Name, s.ServiceLevel, s.EstimatedMinDays, s.EstimatedMaxDays }).ToArray()
            }).Where(x => x.Services.Length > 0).ToArrayAsync(ct);

        var quoteOptions = new List<QuoteOption>();
        var ratePackages = packages.Select(x => new CarrierRatePackage(x.Weight, x.WeightUnit, x.Length, x.Width, x.Height, x.DimensionUnit, x.DeclaredValue)).ToArray();
        foreach (var carrier in carriers)
        {
            if (!providerResolver.TryResolve(carrier.Code, out var provider) || provider is not ICarrierRateProvider rateProvider) continue;
            var eligible = carrier.Services.Select(x => new EligibleCarrierService(x.Code, x.ServiceLevel, x.EstimatedMinDays, x.EstimatedMaxDays)).ToArray();
            var results = await rateProvider.GetRatesAsync(new(
                new(origin.CountryCode, origin.City, origin.StateOrProvince, origin.PostalCode, origin.AddressLine1),
                new(destination.CountryCode, destination.City, destination.StateOrProvince, destination.PostalCode, destination.AddressLine1),
                ratePackages, currency, request.RequiresCod, eligible), ct);
            var serviceByCode = carrier.Services.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<Guid>();
            foreach (var result in results)
            {
                if (!serviceByCode.TryGetValue(result.ServiceCode, out var service) || !seen.Add(service.Id))
                    return Fail<ShippingQuoteResponse>(ServiceError.Conflict, "Carrier returned an invalid or duplicate service rate.");
                try
                {
                    var option = new QuoteOption(carrier.Id, carrier.Code, carrier.Name, service.Id, service.Code, service.Name,
                        service.ServiceLevel, result.Amount, result.Currency, result.EstimatedMinDays, result.EstimatedMaxDays, result.ProviderReference);
                    if (!string.Equals(option.Currency, currency, StringComparison.Ordinal))
                        return Fail<ShippingQuoteResponse>(ServiceError.Conflict, "Carrier returned a rate in an unexpected currency.");
                    if (option.EstimatedMinDays != service.EstimatedMinDays || option.EstimatedMaxDays != service.EstimatedMaxDays)
                        return Fail<ShippingQuoteResponse>(ServiceError.Conflict, "Carrier returned an invalid delivery estimate.");
                    quoteOptions.Add(option);
                }
                catch (ArgumentException) { return Fail<ShippingQuoteResponse>(ServiceError.Conflict, "Carrier returned an invalid rate."); }
            }
        }
        if (quoteOptions.Count == 0) return Fail<ShippingQuoteResponse>(ServiceError.Conflict, "No eligible shipping rate option is available.");
        ShippingQuote quote;
        try
        {
            var now = timeProvider.GetUtcNow();
            quote = new(merchantId, origin, destination, currency, now, now.AddMinutes(options.Value.LifetimeMinutes), packages, quoteOptions);
        }
        catch (ArgumentException ex) { return Fail<ShippingQuoteResponse>(ServiceError.Validation, ex.Message); }
        dbContext.ShippingQuotes.Add(quote);
        await dbContext.SaveChangesAsync(ct);
        return ServiceResult<ShippingQuoteResponse>.Success(Map(quote, timeProvider.GetUtcNow()));
    }

    public async Task<ServiceResult<ShippingQuoteResponse>> GetAsync(Guid quoteId, CancellationToken ct)
    {
        if (currentUser.MerchantId is not { } merchantId) return Fail<ShippingQuoteResponse>(ServiceError.Forbidden, "Merchant context is required.");
        var quote = await Query().SingleOrDefaultAsync(x => x.Id == quoteId && x.MerchantId == merchantId, ct);
        return quote is null ? Fail<ShippingQuoteResponse>(ServiceError.NotFound, "Shipping quote was not found.")
            : ServiceResult<ShippingQuoteResponse>.Success(Map(quote, timeProvider.GetUtcNow()));
    }

    public async Task<ServiceResult<QuoteHistoryResponse>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (currentUser.MerchantId is not { } merchantId) return Fail<QuoteHistoryResponse>(ServiceError.Forbidden, "Merchant context is required.");
        if (page < 1 || pageSize is < 1 or > 100) return Fail<QuoteHistoryResponse>(ServiceError.Validation, "Page must be at least 1 and pageSize must be between 1 and 100.");
        var baseQuery = dbContext.ShippingQuotes.AsNoTracking().Where(x => x.MerchantId == merchantId);
        var total = await baseQuery.CountAsync(ct);
        // EF Core's SQLite provider cannot translate ORDER BY for DateTimeOffset. Integration tests therefore
        // retain database-side pagination with deterministic Id ordering, but do not validate production chronology.
        // SQL Server uses the public newest-first contract below: CreatedAtUtc DESC, then Id DESC.
        var ordered = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? baseQuery.OrderByDescending(x => x.Id)
            : baseQuery.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id);
        var quotes = await ordered.Include(x => x.Options).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var now = timeProvider.GetUtcNow();
        return ServiceResult<QuoteHistoryResponse>.Success(new(quotes.Select(x => Map(x, now)).ToArray(), page, pageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)));
    }

    private IQueryable<ShippingQuote> Query() => dbContext.ShippingQuotes.AsNoTracking().Include(x => x.Options);
    private static ShippingQuoteResponse Map(ShippingQuote x, DateTimeOffset now)
    {
        var expired = now >= x.ExpiresAtUtc;
        return new(x.Id, Map(x.Origin), Map(x.Destination), x.Currency, expired ? ShippingQuoteStatus.Expired : ShippingQuoteStatus.Active,
            expired, x.CreatedAtUtc, x.ExpiresAtUtc, x.Options.OrderBy(o => o.Amount).ThenBy(o => o.Id).Select(o => new QuoteOptionResponse(
                o.Id, o.CarrierId, o.CarrierCode, o.CarrierName, o.CarrierServiceId, o.ServiceCode, o.ServiceName,
                o.ServiceLevel, o.Amount, o.Currency, o.EstimatedMinDays, o.EstimatedMaxDays)).ToArray());
    }
    private static QuoteAddressResponse Map(QuoteAddress x) => new(x.CountryCode, x.City, x.StateOrProvince, x.PostalCode, x.AddressLine1);
    private static ServiceResult<T> Fail<T>(ServiceError error, string detail) => ServiceResult<T>.Fail(error, detail);
}
