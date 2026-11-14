namespace MoscoRun.Domain.Workouts;

public static class SeriesMetricInfo
{
    /// <summary>Unità di misura canonica della grandezza.</summary>
    public static string Unit(this SeriesMetric metric) => metric switch
    {
        SeriesMetric.HeartRate => "bpm",
        SeriesMetric.Speed => "m/s",
        SeriesMetric.Cadence => "1/min",
        SeriesMetric.Power => "W",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Grandezza sconosciuta."),
    };

    /// <summary>Intervallo fisicamente plausibile: valori fuori sono errori della fonte, non dati da salvare.</summary>
    public static (double Min, double Max) Range(this SeriesMetric metric) => metric switch
    {
        SeriesMetric.HeartRate => (20, 260),
        SeriesMetric.Speed => (0, 100),
        SeriesMetric.Cadence => (0, 400),
        SeriesMetric.Power => (0, 3000),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Grandezza sconosciuta."),
    };
}
