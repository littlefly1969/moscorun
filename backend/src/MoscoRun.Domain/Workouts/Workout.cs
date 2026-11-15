using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using MoscoRun.Domain.Common;

namespace MoscoRun.Domain.Workouts;

/// <summary>
/// Allenamento svolto. Il tracciato e le serie sono salvati a parte (<see cref="WorkoutRoute"/>,
/// <see cref="WorkoutSeries"/>) perché gli elenchi non devono caricarli.
/// </summary>
public sealed class Workout
{
    public const int SourceMaxLength = 32;
    public const int SourceIdMaxLength = 128;
    public const int ActivityTypeMaxLength = 64;
    public const int MaxTrackPoints = 100_000;
    public const int MaxSeriesSamples = 100_000;
    public const double MaxDistanceM = 1_000_000;
    public const double MaxEnergyKcal = 50_000;

    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(7);
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Margine per campioni registrati poco prima o dopo i limiti dichiarati della sessione.</summary>
    public static readonly TimeSpan SampleTolerance = TimeSpan.FromMinutes(10);

    public static readonly DateTimeOffset Epoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly SearchValues<char> SourceChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789-");
    private static readonly SearchValues<char> IdChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.:");

    private Workout()
    {
        Source = SourceId = ActivityType = string.Empty;
        ContentHash = [];
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    /// <summary>Ultimo dispositivo che ha inviato questa versione.</summary>
    public Guid DeviceId { get; private set; }

    public string Source { get; private set; }

    public string SourceId { get; private set; }

    public long Revision { get; private set; }

    public string ActivityType { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset EndedAt { get; private set; }

    public int? UtcOffsetMinutes { get; private set; }

    public TimeSpan? ActiveDuration { get; private set; }

    public double? DistanceM { get; private set; }

    public double? EnergyKcal { get; private set; }

    public double? AverageHeartRate { get; private set; }

    public double? MaxHeartRate { get; private set; }

    public int TrackPointCount { get; private set; }

    public double? MinLatitude { get; private set; }

    public double? MinLongitude { get; private set; }

    public double? MaxLatitude { get; private set; }

    public double? MaxLongitude { get; private set; }

    /// <summary>SHA-256 del contenuto normalizzato: distingue un reinvio identico da una modifica.</summary>
    public byte[] ContentHash { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public GeoBounds? Bounds => MinLatitude is { } a && MinLongitude is { } b && MaxLatitude is { } c && MaxLongitude is { } d
        ? new GeoBounds(a, b, c, d)
        : null;

    public bool HasTrack => TrackPointCount > 0;

    /// <summary>Valida e normalizza un allenamento ricevuto.</summary>
    public static Result<WorkoutContent> Create(Guid ownerId, Guid deviceId, WorkoutRecording recording, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var errors = Validate(recording, now);
        if (errors.Count > 0)
        {
            return Result<WorkoutContent>.Failure(errors);
        }

        var workout = new Workout
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            Source = recording.Source,
            SourceId = recording.SourceId,
            ReceivedAt = TrimToMicroseconds(now),
        };
        return Result<WorkoutContent>.Success(workout.Apply(deviceId, recording, now));
    }

    /// <summary>Esito del confronto fra la versione salvata e una nuova ricezione con la stessa chiave.</summary>
    public WorkoutReplay CompareWith(WorkoutContent incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (ContentHash.AsSpan().SequenceEqual(incoming.Workout.ContentHash))
        {
            return WorkoutReplay.Identical;
        }

        return incoming.Workout.Revision > Revision ? WorkoutReplay.Newer : WorkoutReplay.Conflict;
    }

    /// <summary>Sostituisce il contenuto con quello di una revisione più recente già validata.</summary>
    public WorkoutContent ReplaceWith(WorkoutContent newer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newer);
        if (newer.Workout.Revision <= Revision)
        {
            throw new InvalidOperationException("Solo una revisione più recente può sostituire l'allenamento.");
        }

        return Apply(newer.Workout.DeviceId, newer.Recording, now);
    }

    private WorkoutContent Apply(Guid deviceId, WorkoutRecording recording, DateTimeOffset now)
    {
        var track = recording.Track.Select(TrackCodec.Normalize).OrderBy(p => p.Time).ToArray();
        var series = recording.Series
            .Where(s => s.Value.Count > 0)
            .OrderBy(s => s.Key)
            .Select(s => new WorkoutSeries(Id, s.Key, s.Value.Select(SeriesCodec.Normalize).OrderBy(x => x.Time).ToArray()))
            .ToArray();
        var route = track.Length > 0 ? new WorkoutRoute(Id, track) : null;
        var bounds = GeoBounds.Of(track);

        DeviceId = deviceId;
        Revision = recording.Revision;
        ActivityType = recording.ActivityType;
        StartedAt = TrimToMicroseconds(recording.StartedAt);
        EndedAt = TrimToMicroseconds(recording.EndedAt);
        UtcOffsetMinutes = recording.UtcOffsetMinutes;
        ActiveDuration = recording.ActiveDuration is { } d ? TimeSpan.FromMilliseconds(Math.Round(d.TotalMilliseconds)) : null;
        DistanceM = Round(recording.DistanceM, 2);
        EnergyKcal = Round(recording.EnergyKcal, 2);
        AverageHeartRate = Round(recording.AverageHeartRate, 1);
        MaxHeartRate = Round(recording.MaxHeartRate, 1);
        TrackPointCount = track.Length;
        (MinLatitude, MinLongitude, MaxLatitude, MaxLongitude) =
            (bounds?.MinLatitude, bounds?.MinLongitude, bounds?.MaxLatitude, bounds?.MaxLongitude);
        UpdatedAt = TrimToMicroseconds(now);
        ContentHash = ComputeHash(route, series);

        return new WorkoutContent(this, route, series, recording);
    }

    private byte[] ComputeHash(WorkoutRoute? route, WorkoutSeries[] series)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Text(string value)
        {
            Number(value.Length);
            hash.AppendData(Encoding.UTF8.GetBytes(value));
        }

        void Number(long value) => hash.AppendData(BitConverter.GetBytes(value));
        void Optional(double? value)
        {
            hash.AppendData([value is null ? (byte)0 : (byte)1]);
            hash.AppendData(BitConverter.GetBytes(value ?? 0));
        }

        Number(1); // versione dello schema dell'impronta
        Text(ActivityType);
        Number(StartedAt.UtcTicks);
        Number(EndedAt.UtcTicks);
        Optional(UtcOffsetMinutes);
        Optional(ActiveDuration?.TotalMilliseconds);
        Optional(DistanceM);
        Optional(EnergyKcal);
        Optional(AverageHeartRate);
        Optional(MaxHeartRate);
        var routeData = route?.Data ?? [];
        Number(routeData.Length);
        hash.AppendData(routeData);
        Number(series.Length);
        foreach (var s in series)
        {
            Number((long)s.Metric);
            Number(s.Data.Length);
            hash.AppendData(s.Data);
        }

        return hash.GetHashAndReset();
    }

    private static double? Round(double? value, int digits) =>
        value is { } v ? Math.Round(v, digits, MidpointRounding.ToEven) : null;

    /// <summary>Stessa precisione di PostgreSQL <c>timestamptz</c>: l'impronta non cambia dopo un salvataggio.</summary>
    private static DateTimeOffset TrimToMicroseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static List<DomainError> Validate(WorkoutRecording r, DateTimeOffset now)
    {
        var errors = new List<DomainError>();
        void Add(string code, string message, string field) => errors.Add(new DomainError(code, message, field));

        if (string.IsNullOrEmpty(r.Source) || r.Source.Length > SourceMaxLength || r.Source.AsSpan().ContainsAnyExcept(SourceChars))
        {
            Add("workout.source.invalid", $"source deve avere 1-{SourceMaxLength} caratteri tra lettere minuscole, cifre e -.", "source");
        }

        if (string.IsNullOrEmpty(r.SourceId) || r.SourceId.Length > SourceIdMaxLength || r.SourceId.AsSpan().ContainsAnyExcept(IdChars))
        {
            Add("workout.source_id.invalid", $"sourceId deve avere 1-{SourceIdMaxLength} caratteri tra lettere, cifre e - _ . :", "sourceId");
        }

        if (string.IsNullOrEmpty(r.ActivityType) || r.ActivityType.Length > ActivityTypeMaxLength || r.ActivityType.AsSpan().ContainsAnyExcept(IdChars))
        {
            Add("workout.activity_type.invalid", $"activityType deve avere 1-{ActivityTypeMaxLength} caratteri tra lettere, cifre e - _ . :", "activityType");
        }

        if (r.Revision < 0)
        {
            Add("workout.revision.invalid", "revision non può essere negativa.", "revision");
        }

        if (r.EndedAt <= r.StartedAt || r.EndedAt - r.StartedAt > MaxDuration)
        {
            Add("workout.time.invalid", "endedAt deve seguire startedAt e la durata non può superare 7 giorni.", "endedAt");
        }

        if (r.StartedAt < Epoch)
        {
            Add("workout.time.too_old", "startedAt è anteriore al 2000.", "startedAt");
        }

        if (r.EndedAt > now + MaxClockSkew)
        {
            Add("workout.time.in_future", "L'allenamento termina nel futuro: controllare l'orologio del telefono.", "endedAt");
        }

        if (r.UtcOffsetMinutes is < -14 * 60 or > 14 * 60)
        {
            Add("workout.utc_offset.invalid", "utcOffsetMinutes deve essere compreso tra -840 e 840.", "utcOffsetMinutes");
        }

        if (r.ActiveDuration is { } active && (active < TimeSpan.Zero || active > r.EndedAt - r.StartedAt + TimeSpan.FromSeconds(1)))
        {
            Add("workout.active_duration.invalid", "activeDurationSeconds non può superare la durata della sessione.", "activeDurationSeconds");
        }

        CheckRange(r.DistanceM, 0, MaxDistanceM, "workout.distance.out_of_range", "distanceM", errors);
        CheckRange(r.EnergyKcal, 0, MaxEnergyKcal, "workout.energy.out_of_range", "energyKcal", errors);
        var (hrMin, hrMax) = SeriesMetric.HeartRate.Range();
        CheckRange(r.AverageHeartRate, hrMin, hrMax, "workout.heart_rate.out_of_range", "averageHeartRate", errors);
        CheckRange(r.MaxHeartRate, hrMin, hrMax, "workout.heart_rate.out_of_range", "maxHeartRate", errors);

        var earliest = r.StartedAt - SampleTolerance;
        var latest = r.EndedAt + SampleTolerance;
        ValidateTrack(r.Track, earliest, latest, errors);
        ValidateSeries(r.Series, earliest, latest, errors);
        return errors;
    }

    private static void ValidateTrack(IReadOnlyList<TrackPoint> track, DateTimeOffset earliest, DateTimeOffset latest, List<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.Count > MaxTrackPoints)
        {
            errors.Add(new DomainError("workout.track.too_long", $"Il tracciato supera {MaxTrackPoints} punti.", "track"));
            return;
        }

        for (var i = 0; i < track.Count; i++)
        {
            var p = track[i];
            var valid = p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180
                && p.AltitudeM is null or (>= -1000 and <= 10000)
                && p.AccuracyM is null or (>= 0 and <= 100_000)
                && p.Time >= earliest && p.Time <= latest;
            if (!valid)
            {
                // Un solo errore con l'indice del primo punto non valido: la risposta resta piccola.
                errors.Add(new DomainError(
                    "workout.track.point_invalid",
                    $"Punto {i} non valido: coordinate, altitudine, precisione o istante fuori intervallo.",
                    "track"));
                return;
            }
        }
    }

    private static void ValidateSeries(
        IReadOnlyDictionary<SeriesMetric, IReadOnlyList<SeriesSample>> series,
        DateTimeOffset earliest,
        DateTimeOffset latest,
        List<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(series);
        foreach (var (metric, samples) in series)
        {
            if (!Enum.IsDefined(metric))
            {
                errors.Add(new DomainError("workout.series.metric_invalid", "Grandezza della serie sconosciuta.", "series"));
                continue;
            }

            var field = $"series.{metric}";
            if (samples.Count > MaxSeriesSamples)
            {
                errors.Add(new DomainError("workout.series.too_long", $"La serie supera {MaxSeriesSamples} campioni.", field));
                continue;
            }

            var (min, max) = metric.Range();
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (!double.IsFinite(s.Value) || s.Value < min || s.Value > max || s.Time < earliest || s.Time > latest)
                {
                    errors.Add(new DomainError(
                        "workout.series.sample_invalid",
                        $"Campione {i} non valido: valore fuori da {min}-{max} {metric.Unit()} o istante fuori dalla sessione.",
                        field));
                    break;
                }
            }
        }
    }

    private static void CheckRange(double? value, double min, double max, string code, string field, List<DomainError> errors)
    {
        if (value is { } v && (!double.IsFinite(v) || v < min || v > max))
        {
            errors.Add(new DomainError(code, $"{field} deve essere compreso tra {min} e {max}.", field));
        }
    }
}
