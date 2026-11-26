namespace MoscoRun.Domain.Analysis;

/// <summary>Analisi di una singola revisione lossless di intervalli NN consecutivi, senza correzione implicita.</summary>
public static class RrChunkEngine
{
    public const string MethodVersion = "rr-chunk-beat-end-continuity-v1";
    public const double TimestampToleranceMilliseconds = 2;

    public static bool ValidContext(string context) => context is "resting" or "exercise";

    public static RrChunkAnalysis? Analyze(IReadOnlyList<SignalSample> samples, string context,
        bool screened, bool timestampsAreBeatEnds, ObservationMethod method)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (!ValidContext(context) || samples.Count is < 1 or > 16384)
        {
            return null;
        }

        var span = (samples[^1].Time - samples[0].Time).TotalSeconds;
        if (!screened || !timestampsAreBeatEnds)
        {
            return Report(context, "unconfirmed-protocol", samples.Count, span, null, null, null);
        }

        if (method is not (ObservationMethod.Measured or ObservationMethod.ProviderReported))
        {
            return Report(context, "unsupported-method", samples.Count, span, null, null, null);
        }

        if (samples.Any(item => item.Value is not { } value || !double.IsFinite(value) || value < 250 || value > 2500))
        {
            return Report(context, "invalid-interval", samples.Count, span, null, null, null);
        }

        for (var index = 1; index < samples.Count; index++)
        {
            var elapsed = (samples[index].Time - samples[index - 1].Time).TotalMilliseconds;
            if (elapsed <= 0 || Math.Abs(elapsed - samples[index].Value.GetValueOrDefault()) > TimestampToleranceMilliseconds)
            {
                return Report(context, "discontinuous-beats", samples.Count, span, null, null, null);
            }
        }

        var intervals = samples.Select(item => item.Value.GetValueOrDefault()).ToArray();
        var duration = intervals.Sum() / 1000;
        if (duration < 60 || duration > 1200)
        {
            return Report(context, "unsupported-duration", samples.Count, span, duration, null, null);
        }

        var resting = string.Equals(context, "resting", StringComparison.Ordinal);
        if (resting && CardiacDynamics.ValidateRr(intervals) is { } reason)
        {
            return Report(context, reason, samples.Count, span, duration, null, null);
        }

        var hrv = resting ? CardiacDynamics.SummarizeRr(intervals) : null;
        var dfa = DfaAlpha1Engine.Estimate(intervals, artifactScreened: true);
        var status = hrv is not null || dfa is not null ? "observed" : "insufficient-support";
        return Report(context, status, samples.Count, span, duration, hrv, dfa);
    }

    private static RrChunkAnalysis Report(string context, string status, int count, double span, double? duration,
        HrvSummary? hrv, DfaAlpha1Result? dfa) =>
        new(MethodVersion, context, status, count, span, duration, CardiacDynamics.HrvMethodVersion, hrv,
            DfaAlpha1Engine.MethodVersion, dfa);
}
