namespace MoscoRun.Domain.Analysis;

/// <summary>Simulatore condizionale deterministico della risposta cardiaca. Non è una prescrizione.</summary>
public static class AthleteDigitalTwin
{
    public const string MethodVersion = "conditional-hr-twin-v1";

    public static IReadOnlyList<TwinSegmentResult>? Simulate(
        IReadOnlyList<TwinSegment> segments, double initialBpm, TwinParameters parameters, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!Valid(parameters, asOf) || !InRange(initialBpm, parameters) || segments.Count == 0 ||
            segments.Any(segment => !Positive(segment.DurationSeconds) ||
                !InRange(segment.EquilibriumHeartRateBpm, parameters) ||
                !double.IsFinite(segment.ExternalSpeedMetersPerSecond) || segment.ExternalSpeedMetersPerSecond < 0))
        {
            return null;
        }

        var result = new List<TwinSegmentResult>(segments.Count);
        double seconds = 0;
        double meters = 0;
        var current = initialBpm;
        foreach (var segment in segments)
        {
            var ratio = segment.DurationSeconds / parameters.HeartRateTimeConstantSeconds;
            var decay = Math.Exp(-ratio);
            // expm1(x) con sviluppo locale evita cancellazione numerica per segmenti molto brevi.
            var averageFactor = ratio < 1e-5 ? 1 - ratio / 2 + ratio * ratio / 6 : (1 - decay) / ratio;
            var equilibrium = segment.EquilibriumHeartRateBpm;
            var end = equilibrium + (current - equilibrium) * decay;
            var mean = equilibrium + (current - equilibrium) * averageFactor;
            seconds += segment.DurationSeconds;
            meters += segment.DurationSeconds * segment.ExternalSpeedMetersPerSecond;
            if (!double.IsFinite(seconds) || !double.IsFinite(meters) || !double.IsFinite(mean) || !double.IsFinite(end))
            {
                return null;
            }

            result.Add(new TwinSegmentResult(seconds, meters, end, mean));
            current = end;
        }

        return result.AsReadOnly();
    }

    private static bool Valid(TwinParameters parameters, DateTimeOffset asOf) =>
        !string.IsNullOrWhiteSpace(parameters.CalibrationVersion) && parameters.CalibrationCutoff <= asOf &&
        Positive(parameters.HeartRateTimeConstantSeconds) && Positive(parameters.RestingHeartRateBpm) &&
        Positive(parameters.MaximumHeartRateBpm) && parameters.MaximumHeartRateBpm > parameters.RestingHeartRateBpm;

    private static bool InRange(double bpm, TwinParameters parameters) =>
        double.IsFinite(bpm) && bpm >= parameters.RestingHeartRateBpm && bpm <= parameters.MaximumHeartRateBpm;

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
