namespace MoscoRun.Domain.Analysis;

/// <summary>Valuta predizioni emesse prima dell'esito; non attribuisce causalità al carico.</summary>
public static class AdaptationEngine
{
    public static AdaptationAssessment? Evaluate(
        IEnumerable<AdaptationObservation> observations, DateTimeOffset cutoff, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (asOf <= cutoff)
        {
            return null;
        }

        var eligible = observations.Where(item => item.Time > cutoff && item.Time <= asOf &&
            item.AvailableAt >= item.Time && item.AvailableAt <= asOf).ToArray();
        if (eligible.Length < 3 || eligible.Select(item => item.Time).Distinct().Count() != eligible.Length ||
            eligible.Any(item => !double.IsFinite(item.Response) || !double.IsFinite(item.Predicted)))
        {
            return null;
        }

        var errors = eligible.Select(item => item.Response - item.Predicted).ToArray();
        var mean = errors.Average();
        var mae = errors.Average(Math.Abs);
        var rmse = Math.Sqrt(errors.Average(error => error * error));
        return double.IsFinite(mean) && double.IsFinite(mae) && double.IsFinite(rmse)
            ? new AdaptationAssessment(errors.Length, mean, mae, rmse) : null;
    }
}
