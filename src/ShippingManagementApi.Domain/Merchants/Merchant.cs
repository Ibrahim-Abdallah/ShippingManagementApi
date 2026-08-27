namespace ShippingManagementApi.Domain.Merchants;

public sealed class Merchant
{
    public const int MaximumNameLength = 200;
    public const int MaximumCodeLength = 50;

    private Merchant() { }

    public Merchant(string name, string code, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = NormalizeName(name);
        Code = NormalizeCode(code);
        IsActive = true;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > MaximumCodeLength || normalized.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException($"Merchant code must be at most {MaximumCodeLength} characters and contain only letters, numbers, '-' or '_'.", nameof(code));
        return normalized;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > MaximumNameLength)
            throw new ArgumentException($"Merchant name must be at most {MaximumNameLength} characters.", nameof(name));
        return normalized;
    }
}
