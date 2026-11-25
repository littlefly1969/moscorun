using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class DurabilityEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(4, 160, 3.6, 160, 10)]
    [InlineData(250, 150, 225, 150, 10)]
    [InlineData(4, 150, 4, 150, 0)]
    [InlineData(4, 160, 4.4, 160, -10)]
    public void Compares_efficiency_for_speed_or_power(
        double earlyLoad, double earlyHr, double lateLoad, double lateHr, double expected)
    {
        var result = DurabilityEngine.Compare(Window(0, earlyLoad, earlyHr), Window(10, lateLoad, lateHr), 0.8);
        result.Should().NotBeNull();
        result!.EfficiencyLossPercent.Should().BeApproximately(expected, 0.000001);
    }

    [Fact]
    public void Missing_paired_support_returns_no_estimate()
    {
        var late = Window(10, 4, 160) with { PairedDuration = TimeSpan.FromMinutes(2) };
        DurabilityEngine.Compare(Window(0, 4, 150), late, 0.8).Should().BeNull();
    }

    [Fact]
    public void Overlapping_windows_are_rejected()
    {
        DurabilityEngine.Compare(Window(0, 4, 150), Window(5, 4, 160), 0.8).Should().BeNull();
    }

    [Fact]
    public void Nonphysical_heart_rate_returns_no_estimate()
    {
        DurabilityEngine.Compare(Window(0, 4, 0), Window(10, 4, 160), 0.8).Should().BeNull();
    }

    [Fact]
    public void Impossible_coverage_is_rejected()
    {
        var late = Window(10, 4, 160) with { PairedDuration = TimeSpan.FromMinutes(11) };
        DurabilityEngine.Compare(Window(0, 4, 150), late, 0.8).Should().BeNull();
    }

    private static DurabilityWindow Window(int offset, double load, double hr) =>
        new(Now.AddMinutes(offset), Now.AddMinutes(offset + 10), TimeSpan.FromMinutes(10), load, hr);
}
