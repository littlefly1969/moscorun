using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class ObservedPerformanceEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Finds_exact_window_even_between_sample_boundaries()
    {
        SignalSample[] samples = [new(Start, 1), new(Start.AddSeconds(3), 6),
            new(Start.AddSeconds(7), 2), new(Start.AddSeconds(10), 2)];
        var points = ObservedPerformanceEngine.Build(samples, Start, Start.AddSeconds(10),
            TimeSpan.FromSeconds(5), [5, 10, 11]);
        points[0].Mean.Should().BeApproximately(5.2, 1e-10);
        points[0].Start.Should().Be(Start.AddSeconds(3));
        points[0].End.Should().Be(Start.AddSeconds(8));
        points[1].Mean.Should().BeApproximately(3.3, 1e-10);
        points[2].Mean.Should().BeNull();
        points[2].Start.Should().BeNull();
        points[2].End.Should().BeNull();
    }

    [Fact]
    public void Conflicts_and_gaps_cannot_be_bridged()
    {
        SignalSample[] samples = [new(Start, 3), new(Start.AddSeconds(2), 3),
            new(Start.AddSeconds(3), 9), new(Start.AddSeconds(3), 1),
            new(Start.AddSeconds(4), 4), new(Start.AddSeconds(6), 4),
            new(Start.AddSeconds(20), 8), new(Start.AddSeconds(22), 8)];
        var points = ObservedPerformanceEngine.Build(samples, Start, Start.AddSeconds(22),
            TimeSpan.FromSeconds(2), [2, 4, 5]);
        points[0].Mean.Should().Be(8);
        points[1].Mean.Should().Be(4);
        points[2].Mean.Should().BeNull();
    }

    [Fact]
    public void Recorded_zero_is_not_missing_and_ties_select_earliest_window()
    {
        SignalSample[] samples = [new(Start, 0), new(Start.AddSeconds(2), 0),
            new(Start.AddSeconds(4), 0)];
        var point = ObservedPerformanceEngine.Build(samples, Start, Start.AddSeconds(4),
            TimeSpan.FromSeconds(2), [2]).Single();
        point.Mean.Should().Be(0);
        point.Start.Should().Be(Start);
    }

    [Fact]
    public void Never_extrapolates_past_last_sample()
    {
        var point = ObservedPerformanceEngine.Build([new(Start, 4)], Start, Start.AddMinutes(1),
            TimeSpan.FromSeconds(5), [5]).Single();
        point.Mean.Should().BeNull();
    }
}
