namespace ShippingManagementApi.Domain.Carriers;

public enum ServiceLevel { Economy, Standard, Express, NextDay, SameDay }

public sealed class CarrierService
{
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;

    private CarrierService() { }

    public CarrierService(Guid carrierId, string code, string name, ServiceLevel serviceLevel,
        int estimatedMinDays, int estimatedMaxDays, DateTimeOffset now)
        : this(Guid.NewGuid(), carrierId, code, name, serviceLevel, estimatedMinDays, estimatedMaxDays, now) { }

    public CarrierService(Guid id, Guid carrierId, string code, string name, ServiceLevel serviceLevel,
        int estimatedMinDays, int estimatedMaxDays, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Carrier service identifier is required.", nameof(id));
        if (carrierId == Guid.Empty) throw new ArgumentException("Carrier identifier is required.", nameof(carrierId));
        Id = id;
        CarrierId = carrierId;
        Code = NormalizeCode(code);
        SetDetails(name, serviceLevel, estimatedMinDays, estimatedMaxDays, now);
        IsActive = true;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid CarrierId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ServiceLevel ServiceLevel { get; private set; }
    public bool IsActive { get; private set; }
    public int EstimatedMinDays { get; private set; }
    public int EstimatedMaxDays { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Carrier Carrier { get; private set; } = null!;

    public void Update(string name, ServiceLevel serviceLevel, int estimatedMinDays, int estimatedMaxDays, DateTimeOffset now) =>
        SetDetails(name, serviceLevel, estimatedMinDays, estimatedMaxDays, now);

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
            throw new ArgumentException($"Carrier service code must be at most {MaximumCodeLength} characters and contain only letters, numbers, '-' or '_'.", nameof(code));
        return normalized;
    }

    private void SetDetails(string name, ServiceLevel serviceLevel, int estimatedMinDays, int estimatedMaxDays, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length > MaximumNameLength)
            throw new ArgumentException($"Carrier service name must be at most {MaximumNameLength} characters.", nameof(name));
        if (!Enum.IsDefined(serviceLevel)) throw new ArgumentOutOfRangeException(nameof(serviceLevel), "Service level is invalid.");
        if (estimatedMinDays < 0) throw new ArgumentOutOfRangeException(nameof(estimatedMinDays), "Estimated minimum days must be zero or greater.");
        if (estimatedMaxDays < estimatedMinDays) throw new ArgumentException("Estimated maximum days must be greater than or equal to estimated minimum days.", nameof(estimatedMaxDays));
        Name = normalizedName;
        ServiceLevel = serviceLevel;
        EstimatedMinDays = estimatedMinDays;
        EstimatedMaxDays = estimatedMaxDays;
        UpdatedAtUtc = now;
    }
}
