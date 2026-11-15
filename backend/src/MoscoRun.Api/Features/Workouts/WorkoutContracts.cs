using System.ComponentModel.DataAnnotations;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Api.Features.Workouts;

/// <summary>Allenamento svolto inviato dal telefono. Reinviare lo stesso contenuto è sicuro.</summary>
/// <param name="Source">Fonte, es. <c>samsung-health</c>.</param>
/// <param name="SourceId">Identificativo stabile nella fonte.</param>
/// <param name="Revision">Revisione nella fonte: un valore maggiore aggiorna l'allenamento già ricevuto.</param>
/// <param name="ActivityType">Codice del tipo di attività nella fonte (es. <c>RUNNING</c>).</param>
/// <param name="StartedAt">Inizio (ISO 8601 con offset).</param>
/// <param name="EndedAt">Fine (ISO 8601 con offset).</param>
/// <param name="UtcOffsetMinutes">Fuso orario locale, se noto.</param>
/// <param name="ActiveDurationSeconds">Durata senza pause, se indicata dalla fonte.</param>
/// <param name="DistanceM">Distanza in metri.</param>
/// <param name="EnergyKcal">Energia in kcal.</param>
/// <param name="AverageHeartRate">Frequenza cardiaca media (bpm).</param>
/// <param name="MaxHeartRate">Frequenza cardiaca massima (bpm).</param>
/// <param name="Track">Punti GPS in qualunque ordine; assenti se l'allenamento non ha tracciato.</param>
/// <param name="Series">Serie temporali, al più una per grandezza.</param>
public sealed record IngestWorkoutRequest(
    [property: Required] string? Source,
    [property: Required] string? SourceId,
    [property: Required] long? Revision,
    [property: Required] string? ActivityType,
    [property: Required] DateTimeOffset? StartedAt,
    [property: Required] DateTimeOffset? EndedAt,
    int? UtcOffsetMinutes,
    double? ActiveDurationSeconds,
    double? DistanceM,
    double? EnergyKcal,
    double? AverageHeartRate,
    double? MaxHeartRate,
    IReadOnlyList<TrackPointInput?>? Track,
    IReadOnlyList<SeriesInput?>? Series);

/// <param name="Time">Istante.</param>
/// <param name="Lat">Latitudine WGS84.</param>
/// <param name="Lon">Longitudine WGS84.</param>
/// <param name="AltitudeM">Altitudine in metri.</param>
/// <param name="AccuracyM">Precisione orizzontale in metri.</param>
public sealed record TrackPointInput(DateTimeOffset? Time, double? Lat, double? Lon, double? AltitudeM, double? AccuracyM);

/// <param name="Metric">Grandezza; l'unità è fissata (bpm, m/s, 1/min, W).</param>
/// <param name="Samples">Campioni.</param>
public sealed record SeriesInput(SeriesMetric? Metric, IReadOnlyList<SampleInput?>? Samples);

public sealed record SampleInput(DateTimeOffset? Time, double? Value);

/// <param name="MinLatitude">Latitudine minima.</param>
/// <param name="MinLongitude">Longitudine minima.</param>
/// <param name="MaxLatitude">Latitudine massima.</param>
/// <param name="MaxLongitude">Longitudine massima.</param>
public sealed record BoundsDto(double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude);

/// <summary>Riepilogo di un allenamento, senza tracciato né serie.</summary>
public sealed record WorkoutSummaryDto(
    Guid Id,
    string Source,
    string SourceId,
    long Revision,
    string ActivityType,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int? UtcOffsetMinutes,
    double? ActiveDurationSeconds,
    double? DistanceM,
    double? EnergyKcal,
    double? AverageHeartRate,
    double? MaxHeartRate,
    int TrackPointCount,
    BoundsDto? Bounds,
    DateTimeOffset UpdatedAt)
{
    public static WorkoutSummaryDto From(Workout w) => new(
        w.Id,
        w.Source,
        w.SourceId,
        w.Revision,
        w.ActivityType,
        w.StartedAt,
        w.EndedAt,
        w.UtcOffsetMinutes,
        w.ActiveDuration?.TotalSeconds,
        w.DistanceM,
        w.EnergyKcal,
        w.AverageHeartRate,
        w.MaxHeartRate,
        w.TrackPointCount,
        w.Bounds is { } b ? new BoundsDto(b.MinLatitude, b.MinLongitude, b.MaxLatitude, b.MaxLongitude) : null,
        w.UpdatedAt);
}

public sealed record WorkoutList(IReadOnlyList<WorkoutSummaryDto> Items, DateTimeOffset From, DateTimeOffset To);

/// <param name="Metric">Grandezza.</param>
/// <param name="Unit">Unità di misura.</param>
/// <param name="SampleCount">Numero di campioni.</param>
public sealed record SeriesInfoDto(SeriesMetric Metric, string Unit, int SampleCount);

/// <summary>Dettaglio: riepilogo più l'elenco delle serie disponibili.</summary>
public sealed record WorkoutDetailDto(WorkoutSummaryDto Summary, IReadOnlyList<SeriesInfoDto> Series);

/// <summary>Serie completa: <c>times</c> in millisecondi Unix e <c>values</c> allineati per indice.</summary>
public sealed record SeriesDto(SeriesMetric Metric, string Unit, IReadOnlyList<long> Times, IReadOnlyList<double> Values);

public sealed record WorkoutSeriesList(IReadOnlyList<SeriesDto> Items);

/// <summary>Geometria GeoJSON (RFC 7946): coordinate <c>[longitudine, latitudine]</c> o con altitudine.</summary>
public sealed record LineStringGeometry(string Type, IReadOnlyList<double[]> Coordinates);

/// <param name="WorkoutId">Allenamento.</param>
/// <param name="TotalPoints">Punti salvati.</param>
/// <param name="ReturnedPoints">Punti restituiti dopo la semplificazione.</param>
/// <param name="Times">Istante di ogni coordinata, in millisecondi Unix.</param>
public sealed record RouteProperties(Guid WorkoutId, int TotalPoints, int ReturnedPoints, IReadOnlyList<long> Times);

/// <summary>Tracciato come Feature GeoJSON (RFC 7946), utilizzabile da qualunque libreria di mappe.</summary>
/// <param name="Type">Sempre <c>Feature</c>.</param>
/// <param name="Bbox">[ovest, sud, est, nord].</param>
/// <param name="Geometry">LineString del tracciato.</param>
/// <param name="Properties">Metadati del tracciato.</param>
public sealed record RouteFeature(string Type, double[] Bbox, LineStringGeometry Geometry, RouteProperties Properties);
