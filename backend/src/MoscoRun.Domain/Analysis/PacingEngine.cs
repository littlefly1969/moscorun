namespace MoscoRun.Domain.Analysis;

/// <summary>Allocazione di un tempo-obiettivo su segmenti a costo relativo fornito; nessuna ottimizzazione clinica.</summary>
public static class PacingEngine
{
    public const string MethodVersion = "weighted-split-v1";

    public static IReadOnlyList<PacingSplit>? Allocate(
        IReadOnlyList<PacingSegment> segments, double totalSeconds)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0 || !Positive(totalSeconds) ||
            segments.Any(segment => !Positive(segment.DistanceMeters) || !Positive(segment.RelativeTimeCost)))
        {
            return null;
        }

        var totalWeight = segments.Sum(segment => segment.DistanceMeters * segment.RelativeTimeCost);
        if (!Positive(totalWeight))
        {
            return null;
        }

        var splits = new List<PacingSplit>(segments.Count);
        double cumulative = 0;
        foreach (var segment in segments)
        {
            var duration = totalSeconds * ((segment.DistanceMeters * segment.RelativeTimeCost) / totalWeight);
            cumulative += duration;
            if (!Positive(duration) || !Positive(cumulative))
            {
                return null;
            }

            splits.Add(new PacingSplit(segment.DistanceMeters, duration, cumulative));
        }

        return splits.AsReadOnly();
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
