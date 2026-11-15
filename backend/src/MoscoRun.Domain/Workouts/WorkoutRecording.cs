namespace MoscoRun.Domain.Workouts;

/// <summary>Allenamento svolto così come inviato da una fonte (es. Samsung Health tramite l'app Android).</summary>
/// <param name="Source">Fonte dei dati, es. <c>samsung-health</c> o <c>health-connect</c>.</param>
/// <param name="SourceId">Identificativo stabile nella fonte: chiave di idempotenza insieme alla fonte.</param>
/// <param name="Revision">Revisione nella fonte (es. ultimo aggiornamento in millisecondi): un valore maggiore sostituisce il precedente.</param>
/// <param name="ActivityType">Codice del tipo di attività nella fonte, conservato senza conversioni.</param>
/// <param name="StartedAt">Inizio.</param>
/// <param name="EndedAt">Fine.</param>
/// <param name="UtcOffsetMinutes">Fuso orario locale dell'allenamento, se noto.</param>
/// <param name="ActiveDuration">Durata attiva (senza pause), se indicata dalla fonte.</param>
/// <param name="DistanceM">Distanza in metri, se indicata dalla fonte.</param>
/// <param name="EnergyKcal">Energia spesa in kcal, se indicata dalla fonte.</param>
/// <param name="AverageHeartRate">Frequenza cardiaca media dichiarata dalla fonte.</param>
/// <param name="MaxHeartRate">Frequenza cardiaca massima dichiarata dalla fonte.</param>
/// <param name="Track">Punti GPS, eventualmente vuoto.</param>
/// <param name="Series">Serie temporali per grandezza.</param>
public sealed record WorkoutRecording(
    string Source,
    string SourceId,
    long Revision,
    string ActivityType,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int? UtcOffsetMinutes,
    TimeSpan? ActiveDuration,
    double? DistanceM,
    double? EnergyKcal,
    double? AverageHeartRate,
    double? MaxHeartRate,
    IReadOnlyList<TrackPoint> Track,
    IReadOnlyDictionary<SeriesMetric, IReadOnlyList<SeriesSample>> Series);
