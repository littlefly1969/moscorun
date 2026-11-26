namespace MoscoRun.Domain.Analysis;

/// <summary>Endpoint su sedute indipendenti; non considera campioni adiacenti come repliche.</summary>
public static class ResearchBenchmark
{
    public const string MethodVersion = "paired-holdout-benchmark-v1";

    public static BenchmarkReport? Evaluate(
        IEnumerable<BenchmarkPrediction> predictions, DateTimeOffset cutoff, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(predictions);
        if (asOf <= cutoff)
        {
            return null;
        }

        var rows = predictions.ToArray();
        if (rows.Length < 3 || rows.Any(row => !Valid(row, cutoff, asOf)) ||
            rows.Select(row => (row.OwnerId, row.SessionId)).Distinct().Count() != rows.Length)
        {
            return null;
        }

        var model = Score(rows.Select(row => row.Model - row.Observed));
        var baseline = Score(rows.Select(row => row.Baseline - row.Observed));
        if (model is null || baseline is null)
        {
            return null;
        }

        var intervals = rows.Where(row => row.Lower.HasValue).ToArray();
        double? coverage = intervals.Length == 0 ? null :
            (double)intervals.Count(row => row.Observed >= row.Lower && row.Observed <= row.Upper) / intervals.Length;
        double? width = intervals.Length == 0 ? null : intervals.Average(row => row.Upper!.Value - row.Lower!.Value);
        return new BenchmarkReport(rows.Length, rows.Select(row => row.OwnerId).Distinct().Count(),
            model, baseline, baseline.MeanAbsoluteError - model.MeanAbsoluteError, intervals.Length,
            coverage, width);
    }

    private static bool Valid(BenchmarkPrediction row, DateTimeOffset cutoff, DateTimeOffset asOf) =>
        row.OwnerId != Guid.Empty && row.SessionId != Guid.Empty && IsCausal(row, cutoff, asOf) &&
        IsFinite(row) && HasValidInterval(row);

    /// <summary>Emessa dopo il cutoff, prima del bersaglio, e bersaglio noto entro l'as-of.</summary>
    private static bool IsCausal(BenchmarkPrediction row, DateTimeOffset cutoff, DateTimeOffset asOf) =>
        row.IssuedAt > cutoff && row.IssuedAt < row.TargetAt && row.TargetAt <= row.TargetAvailableAt &&
        row.TargetAvailableAt <= asOf;

    private static bool IsFinite(BenchmarkPrediction row) =>
        double.IsFinite(row.Observed) && double.IsFinite(row.Model) && double.IsFinite(row.Baseline);

    /// <summary>Intervallo assente, oppure con estremi finiti e ordinati (ampiezza finita implica estremi finiti).</summary>
    private static bool HasValidInterval(BenchmarkPrediction row)
    {
        if (!row.Lower.HasValue && !row.Upper.HasValue)
        {
            return true;
        }

        return row.Lower is { } lower && row.Upper is { } upper && lower <= upper && double.IsFinite(upper - lower);
    }

    private static BenchmarkScore? Score(IEnumerable<double> residuals)
    {
        var values = residuals.ToArray();
        var bias = values.Average();
        var mae = values.Average(Math.Abs);
        var rmse = Math.Sqrt(values.Average(error => error * error));
        return double.IsFinite(bias) && double.IsFinite(mae) && double.IsFinite(rmse)
            ? new BenchmarkScore(bias, mae, rmse) : null;
    }
}
