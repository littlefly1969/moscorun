namespace MoscoRun.Domain.Analysis;

public sealed record DailyMetricReport(
    string Metric, string Unit, string Source, DateTimeOffset AsOf, string MethodVersion, int BaselineDays,
    DailyMetricPoint Current, IReadOnlyList<DailyMetricPoint> History, string Status, RecoveryDeviation? Baseline,
    double? CurrentVersusMedianPercent, double? WeightTrendKgPerWeek);
