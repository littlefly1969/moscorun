using System.Buffers;
using MoscoRun.Domain.Common;

namespace MoscoRun.Domain.Measurements;

/// <summary>Pesata definitiva registrata nell'archivio.</summary>
public sealed class Weighing
{
    public const decimal MinWeightKg = 2m;
    public const decimal MaxWeightKg = 300m;
    public const int MinImpedanceOhm = 1;
    public const int MaxImpedanceOhm = 3000;
    public const int SourceEventIdMaxLength = 128;

    /// <summary>Tolleranza per orologi non perfettamente sincronizzati.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    private static readonly SearchValues<char> AllowedIdChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.:");

    private Weighing()
    {
        SourceEventId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string SourceEventId { get; private set; }

    public DateTimeOffset MeasuredAt { get; private set; }

    public decimal WeightKg { get; private set; }

    public int? ImpedanceOhm { get; private set; }

    public AttributionStatus Attribution { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Valida la lettura e crea la pesata. Il peso è arrotondato a 10 g.</summary>
    public static Result<Weighing> Create(Guid ownerId, Guid deviceId, WeighingReading reading, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var errors = Validate(reading, now);
        if (errors.Count > 0)
        {
            return Result<Weighing>.Failure(errors);
        }

        return Result<Weighing>.Success(new Weighing
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            DeviceId = deviceId,
            SourceEventId = reading.SourceEventId,
            MeasuredAt = NormalizeTime(reading.MeasuredAt),
            WeightKg = Normalize(reading.WeightKg),
            ImpedanceOhm = reading.ImpedanceOhm,
            Attribution = AttributionStatus.Assumed,
            ReceivedAt = now.ToUniversalTime(),
        });
    }

    /// <summary>
    /// Vero se una nuova lettura con lo stesso <see cref="SourceEventId"/> descrive la stessa misura:
    /// in tal caso è un reinvio e non un conflitto.
    /// </summary>
    public bool IsSameMeasurementAs(WeighingReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return string.Equals(SourceEventId, reading.SourceEventId, StringComparison.Ordinal)
            && MeasuredAt == NormalizeTime(reading.MeasuredAt)
            && WeightKg == Normalize(reading.WeightKg)
            && ImpedanceOhm == reading.ImpedanceOhm;
    }

    public void SetAttribution(AttributionStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Stato di attribuzione sconosciuto.");
        }

        Attribution = status;
    }

    /// <summary>
    /// UTC con precisione al microsecondo, la stessa di PostgreSQL <c>timestamptz</c>: senza questa
    /// normalizzazione un reinvio identico verrebbe confrontato con il valore troncato dal database.
    /// </summary>
    private static DateTimeOffset NormalizeTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static decimal Normalize(decimal weightKg) =>
        Math.Round(weightKg, 2, MidpointRounding.ToEven);

    private static List<DomainError> Validate(WeighingReading reading, DateTimeOffset now)
    {
        var errors = new List<DomainError>();

        if (string.IsNullOrWhiteSpace(reading.SourceEventId)
            || reading.SourceEventId.Length > SourceEventIdMaxLength
            || reading.SourceEventId.AsSpan().ContainsAnyExcept(AllowedIdChars))
        {
            errors.Add(new DomainError(
                "weighing.source_event_id.invalid",
                $"sourceEventId deve avere 1-{SourceEventIdMaxLength} caratteri tra lettere, cifre e - _ . :",
                "sourceEventId"));
        }

        if (reading.WeightKg is < MinWeightKg or > MaxWeightKg)
        {
            errors.Add(new DomainError(
                "weighing.weight.out_of_range",
                $"Il peso deve essere compreso tra {MinWeightKg} e {MaxWeightKg} kg.",
                "weightKg"));
        }

        if (reading.ImpedanceOhm is { } impedance && impedance is < MinImpedanceOhm or > MaxImpedanceOhm)
        {
            errors.Add(new DomainError(
                "weighing.impedance.out_of_range",
                $"L'impedenza deve essere compresa tra {MinImpedanceOhm} e {MaxImpedanceOhm} ohm.",
                "impedanceOhm"));
        }

        if (reading.MeasuredAt > now + MaxClockSkew)
        {
            errors.Add(new DomainError(
                "weighing.measured_at.in_future",
                "La data della misura è nel futuro: controllare l'orologio dell'agente.",
                "measuredAt"));
        }

        return errors;
    }
}
