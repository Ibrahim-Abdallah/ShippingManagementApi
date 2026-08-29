namespace ShippingManagementApi.Domain.Carriers;

public sealed class Carrier
{
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;

    private Carrier() { }

    public Carrier(string code, string name, bool supportsPickup, bool supportsTracking,
        bool supportsCancellation, bool supportsCod, DateTimeOffset now)
        : this(Guid.NewGuid(), code, name, supportsPickup, supportsTracking, supportsCancellation, supportsCod, now) { }

    public Carrier(Guid id, string code, string name, bool supportsPickup, bool supportsTracking,
        bool supportsCancellation, bool supportsCod, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Carrier identifier is required.", nameof(id));
        Id = id;
        Code = NormalizeCode(code);
        SetDetails(name, supportsPickup, supportsTracking, supportsCancellation, supportsCod, now);
        IsActive = true;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool SupportsPickup { get; private set; }
    public bool SupportsTracking { get; private set; }
    public bool SupportsCancellation { get; private set; }
    public bool SupportsCod { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public ICollection<CarrierService> Services { get; private set; } = new List<CarrierService>();

    public void Update(string name, bool supportsPickup, bool supportsTracking,
        bool supportsCancellation, bool supportsCod, DateTimeOffset now) =>
        SetDetails(name, supportsPickup, supportsTracking, supportsCancellation, supportsCod, now);

    public void SetActivation(bool isActive, DateTimeOffset now)
    {
        IsActive = isActive;
        UpdatedAtUtc = now;
    }

    public static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > MaximumCodeLength || normalized.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException($"Carrier code must be at most {MaximumCodeLength} characters and contain only letters, numbers, '-' or '_'.", nameof(code));
        return normalized;
    }

    private void SetDetails(string name, bool supportsPickup, bool supportsTracking,
        bool supportsCancellation, bool supportsCod, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length > MaximumNameLength)
            throw new ArgumentException($"Carrier name must be at most {MaximumNameLength} characters.", nameof(name));
        Name = normalizedName;
        SupportsPickup = supportsPickup;
        SupportsTracking = supportsTracking;
        SupportsCancellation = supportsCancellation;
        SupportsCod = supportsCod;
        UpdatedAtUtc = now;
    }
}
