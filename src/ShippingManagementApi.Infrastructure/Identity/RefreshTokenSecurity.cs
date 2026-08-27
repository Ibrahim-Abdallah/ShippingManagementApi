using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ShippingManagementApi.Infrastructure.Identity;

public static class RefreshTokenSecurity
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
