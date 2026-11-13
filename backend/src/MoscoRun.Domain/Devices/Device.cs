using MoscoRun.Domain.Common;

namespace MoscoRun.Domain.Devices;

/// <summary>Agente registrato con credenziale propria, revocabile e limitata all'acquisizione.</summary>
public sealed class Device
{
    public const int NameMaxLength = 80;

    private Device()
    {
        Name = string.Empty;
        TokenHash = [];
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; }

    public DeviceKind Kind { get; private set; }

    /// <summary>SHA-256 del segreto: il segreto in chiaro non viene mai salvato.</summary>
    public byte[] TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Ultima richiesta autenticata con il token, riuscita o no: indica che l'agente è vivo e raggiungibile.</summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Ultima pesata accettata da questo dispositivo: indica la freschezza dei dati, non solo del contatto.</summary>
    public DateTimeOffset? LastIngestedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    /// <summary>Registra un dispositivo. L'<c>id</c> è scelto dal chiamante perché è incluso nel token.</summary>
    public static Result<Device> Register(Guid id, Guid ownerId, string name, DeviceKind kind, byte[] tokenHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identificativo vuoto.", nameof(id));
        }

        var errors = new List<DomainError>();
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length is 0 or > NameMaxLength)
        {
            errors.Add(new DomainError("device.name.invalid", $"Il nome deve avere 1-{NameMaxLength} caratteri.", "name"));
        }

        if (!Enum.IsDefined(kind))
        {
            errors.Add(new DomainError("device.kind.invalid", "Tipo di dispositivo sconosciuto.", "kind"));
        }

        if (tokenHash.Length != 32)
        {
            throw new ArgumentException("L'hash del token deve essere SHA-256 (32 byte).", nameof(tokenHash));
        }

        if (errors.Count > 0)
        {
            return Result<Device>.Failure(errors);
        }

        return Result<Device>.Success(new Device
        {
            Id = id,
            OwnerId = ownerId,
            Name = trimmed,
            Kind = kind,
            TokenHash = tokenHash,
            CreatedAt = now.ToUniversalTime(),
        });
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now.ToUniversalTime();

    /// <summary>Registra l'ultimo contatto; scrive al più una volta al minuto per non generare carico inutile.</summary>
    public bool Touch(DateTimeOffset now)
    {
        if (LastSeenAt is { } last && now - last < TimeSpan.FromMinutes(1))
        {
            return false;
        }

        LastSeenAt = now.ToUniversalTime();
        return true;
    }

    /// <summary>Registra un'acquisizione riuscita (prima ricezione o reinvio identico già presente).</summary>
    public void RecordIngestion(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        if (LastIngestedAt is null || utc > LastIngestedAt)
        {
            LastIngestedAt = utc;
        }
    }
}
