using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.Infrastructure.Identity;

internal sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    ShippingManagementDbContext dbContext,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider,
    ICurrentUserContext currentUser) : IAuthenticationService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<ServiceResult<TokenPairResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.Include(x => x.Merchant)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == userManager.NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
            return InvalidCredentials();

        var roles = await userManager.GetRolesAsync(user);
        if (!IsValidAccount(user, roles)) return InvalidCredentials();

        var pair = await IssuePairAsync(user, roles, cancellationToken);
        return ServiceResult<TokenPairResponse>.Success(pair);
    }

    public async Task<ServiceResult<TokenPairResponse>> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return InvalidToken();
        var hash = RefreshTokenSecurity.Hash(rawRefreshToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var token = await dbContext.RefreshTokens.Include(x => x.User).ThenInclude(x => x.Merchant)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (token is null || !token.IsUsable(now) || !token.User.IsActive) return InvalidToken();

        var roles = await userManager.GetRolesAsync(token.User);
        if (!IsValidAccount(token.User, roles)) return InvalidToken();

        token.RevokedAtUtc = now;
        token.Version++;
        var pair = await IssuePairAsync(token.User, roles, cancellationToken, token);
        try
        {
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<TokenPairResponse>.Success(pair);
        }
        catch (DbUpdateConcurrencyException)
        {
            return InvalidToken();
        }
    }

    public async Task<ServiceResult<bool>> LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return InvalidLogoutToken();
        var hash = RefreshTokenSecurity.Hash(rawRefreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (token is null || !token.IsUsable(now)) return InvalidLogoutToken();
        token.RevokedAtUtc = now;
        token.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return InvalidLogoutToken(); }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id) return ServiceResult<CurrentUserResponse>.Fail(ServiceError.InvalidToken, "Authentication is required.");
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || !user.IsActive) return ServiceResult<CurrentUserResponse>.Fail(ServiceError.InvalidToken, "Authentication is required.");
        var roles = await userManager.GetRolesAsync(user);
        return ServiceResult<CurrentUserResponse>.Success(new(id, user.Email!, roles.ToArray(), user.MerchantId));
    }

    private async Task<TokenPairResponse> IssuePairAsync(ApplicationUser user, IList<string> roles,
        CancellationToken cancellationToken, RefreshToken? replacedToken = null)
    {
        var now = timeProvider.GetUtcNow();
        var accessExpiry = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (user.MerchantId is { } merchantId) claims.Add(new("merchant_id", merchantId.ToString()));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime, credentials);
        var rawRefreshToken = RefreshTokenSecurity.Generate();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = RefreshTokenSecurity.Hash(rawRefreshToken),
            CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(_options.RefreshTokenLifetimeDays)
        };
        dbContext.RefreshTokens.Add(refreshToken);
        if (replacedToken is not null) replacedToken.ReplacedByTokenId = refreshToken.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), rawRefreshToken, accessExpiry);
    }

    private static bool IsValidAccount(ApplicationUser user, IList<string> roles) =>
        !roles.Contains(AppRoles.Merchant) || user.MerchantId is not null && user.Merchant is { IsActive: true };

    private static ServiceResult<TokenPairResponse> InvalidCredentials() =>
        ServiceResult<TokenPairResponse>.Fail(ServiceError.InvalidCredentials, "Invalid email or password.");
    private static ServiceResult<TokenPairResponse> InvalidToken() =>
        ServiceResult<TokenPairResponse>.Fail(ServiceError.InvalidToken, "The refresh token is invalid or expired.");
    private static ServiceResult<bool> InvalidLogoutToken() =>
        ServiceResult<bool>.Fail(ServiceError.InvalidToken, "The refresh token is invalid or expired.");
}
