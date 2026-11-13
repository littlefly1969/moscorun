using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class WeightTrendTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 7, 0, 0, TimeSpan.Zero);

    private static WeightPoint P(double daysAgo, decimal kg) => new(Now.AddDays(-daysAgo), kg);

    [Fact]
    public void MovingAverage_is_null_without_data() =>
        WeightTrend.MovingAverage([], Now).Should().BeNull();

    [Fact]
    public void MovingAverage_uses_only_last_seven_days()
    {
        WeightPoint[] points = [P(0, 80m), P(3, 81m), P(6.9, 82m), P(7, 99m), P(10, 99m)];
        WeightTrend.MovingAverage(points, Now).Should().Be(81m);
    }

    [Fact]
    public void Change_compares_smoothed_averages()
    {
        WeightPoint[] points = [P(0, 79m), P(1, 79.5m), P(7.5, 80.5m), P(8, 81m)];
        WeightTrend.Change(points, Now, TimeSpan.FromDays(7)).Should().Be(-1.5m);
    }

    [Fact]
    public void Change_is_null_when_previous_window_is_empty() =>
        WeightTrend.Change([P(0, 79m)], Now, TimeSpan.FromDays(30)).Should().BeNull();
}
