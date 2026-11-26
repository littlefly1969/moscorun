namespace MoscoRun.Domain.Analysis;

/// <summary>Bilancio discreto con deplezione sopra soglia e recupero mono-esponenziale sotto soglia.</summary>
public static class CriticalReserveEngine
{
    public const string MethodVersion = "conditional-critical-reserve-v1";

    public static IReadOnlyList<ReserveState>? Replay(
        IReadOnlyList<ReserveSegment> segments, double criticalIntensity, double capacity, double initialRemaining)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0 || !Positive(criticalIntensity) || !Positive(capacity) ||
            !double.IsFinite(initialRemaining) || initialRemaining < 0 || initialRemaining > capacity ||
            segments.Any(item => !Positive(item.DurationSeconds) || !double.IsFinite(item.Intensity) ||
                item.Intensity < 0 || !Positive(item.RecoveryTimeConstantSeconds)))
        {
            return null;
        }

        var states = new List<ReserveState>(segments.Count);
        var remaining = initialRemaining;
        double elapsed = 0;
        foreach (var item in segments)
        {
            elapsed += item.DurationSeconds;
            if (item.Intensity > criticalIntensity)
            {
                remaining -= (item.Intensity - criticalIntensity) * item.DurationSeconds;
            }
            else if (item.Intensity < criticalIntensity)
            {
                remaining = capacity - (capacity - remaining) * Math.Exp(-item.DurationSeconds / item.RecoveryTimeConstantSeconds);
            }

            if (!double.IsFinite(elapsed) || !double.IsFinite(remaining))
            {
                return null;
            }

            states.Add(new ReserveState(elapsed, Math.Max(0, remaining), remaining <= 0));
            if (remaining <= 0)
            {
                break;
            }
        }

        return states.AsReadOnly();
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
