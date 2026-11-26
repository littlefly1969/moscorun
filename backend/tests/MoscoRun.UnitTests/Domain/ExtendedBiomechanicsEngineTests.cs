using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class ExtendedBiomechanicsEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Derives_duty_ratio_and_balance_only_on_common_support()
    {
        var channels = new[]
        {
            Channel("speed", (0, 3), (5, 4), (10, 4)),
            Channel("cadence", (2, 150), (7, 180), (10, 180)),
            Channel("groundContactTime", (0, 240), (5, 240), (10, 240)),
            Channel("verticalOscillation", (0, 0.09), (5, 0.09), (10, 0.09)),
            Channel("groundContactBalance", (0, 51), (5, 51), (10, 51)),
        };
        var report = ExtendedBiomechanicsEngine.Build(Start, Start.AddSeconds(10), channels, true, true)!;
        Metric(report, "stepLength").WeightedMean.Should().BeApproximately(1.35, 1e-9);
        Metric(report, "dutyFactor").WeightedMean.Should().BeApproximately(32.25, 1e-9);
        Metric(report, "verticalRatio").SupportedSeconds.Should().Be(8);
        Metric(report, "groundContactTimeAsymmetry").WeightedMean.Should().Be(4);
        Metric(report, "groundContactTimeAsymmetry").WeightedCvPercent.Should().BeNull();
        Metric(report, "groundContactTime").Coverage.Should().Be(1);
    }

    [Fact]
    public void Missing_gaps_and_unconfirmed_units_do_not_create_derived_values()
    {
        var channels = new[]
        {
            Channel("speed", (0, 3), (10, 3)),
            Channel("cadence", (0, 180), (5, 180), (10, 180)),
            Channel("groundContactBalance", (0, 51), (5, 51), (10, 51)),
        };
        var report = ExtendedBiomechanicsEngine.Build(Start, Start.AddSeconds(10), channels, true, false)!;
        Metric(report, "stepLength").WeightedMean.Should().BeNull();
        Metric(report, "groundContactTimeAsymmetry").WeightedMean.Should().BeNull();
        ExtendedBiomechanicsEngine.Build(Start, Start.AddSeconds(10), [channels[0], channels[0]], true, true)
            .Should().BeNull();
    }

    private static BiomechanicsMetricSummary Metric(IReadOnlyList<BiomechanicsMetricSummary> report, string metric) =>
        report.Single(item => string.Equals(item.Metric, metric, StringComparison.Ordinal));

    private static BiomechanicsChannel Channel(string metric, params (int Offset, double Value)[] points) =>
        new(metric, points.Select(point => new SignalSample(Start.AddSeconds(point.Offset), point.Value)).ToArray());
}
