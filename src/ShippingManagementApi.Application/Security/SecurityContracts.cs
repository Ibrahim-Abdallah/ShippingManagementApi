using System.ComponentModel.DataAnnotations;

namespace ShippingManagementApi.Application.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Merchant = "Merchant";
    public static readonly string[] All = [Admin, Operator, Merchant];
}

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string OperatorOrAdmin = "OperatorOrAdmin";
    public const string MerchantOnly = "MerchantOnly";
}

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? MerchantId { get; }
    IReadOnlyCollection<string> Roles { get; }
}

public sealed record LoginRequest([property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required] string Password);
public sealed record RefreshRequest([property: Required] string RefreshToken);
public sealed record LogoutRequest([property: Required] string RefreshToken);
public sealed record TokenPairResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc);
public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyCollection<string> Roles, Guid? MerchantId);

public enum ServiceError { None, InvalidCredentials, InvalidToken, Forbidden, NotFound, Conflict, Validation }
public sealed record ServiceResult<T>(T? Value, ServiceError Error = ServiceError.None, string? Detail = null)
{
    public bool IsSuccess => Error == ServiceError.None;
    public static ServiceResult<T> Success(T value) => new(value);
    public static ServiceResult<T> Fail(ServiceError error, string detail) => new(default, error, detail);
}

public interface IAuthenticationService
{
    Task<ServiceResult<TokenPairResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TokenPairResponse>> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken);
    Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);
}
