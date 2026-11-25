using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class SignalProcessingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Audit_caps_support_across_gaps()
    {
        var result = SignalProcessing.Audit([At(0, 100), At(2, 120), At(10, 140)],
            Now, Now.AddSeconds(10), TimeSpan.FromSeconds(3));
        result.SupportedDuration.Should().Be(TimeSpan.FromSeconds(5));
        result.Coverage.Should().Be(0.5);
        result.TimeWeightedMean.Should().Be(112);
    }

    [Fact]
    public void Conflicting_duplicate_does_not_support_interval()
    {
        var result = SignalProcessing.Audit([At(0, 100), At(2, 120), At(2, 150), At(4, 130)],
            Now, Now.AddSeconds(4), TimeSpan.FromSeconds(5));
        result.ConflictingCount.Should().Be(1);
        result.DuplicateCount.Should().Be(1);
        result.Coverage.Should().Be(0);
        result.TimeWeightedMean.Should().BeNull();
    }

    [Fact]
    public void Causal_alignment_excludes_future_and_stale_values()
    {
        SignalSample[] samples = [At(0, 100), At(5, 150)];
        SignalProcessing.CausalValue(samples, Now.AddSeconds(2), TimeSpan.FromSeconds(3)).Should().Be(100);
        SignalProcessing.CausalValue(samples, Now.AddSeconds(4), TimeSpan.FromSeconds(3)).Should().BeNull();
    }

    [Fact]
    public void Causal_alignment_rejects_latest_conflict()
    {
        SignalProcessing.CausalValue([At(0, 100), At(1, 120), At(1, 130)],
            Now.AddSeconds(2), TimeSpan.FromSeconds(5)).Should().BeNull();
    }

    [Fact]
    public void Median_handles_outliers_and_missing()
    {
        SignalProcessing.Median([1, 2, 3, 1000, double.NaN]).Should().Be(2.5);
        SignalProcessing.Median([double.NaN]).Should().BeNull();
    }

    private static SignalSample At(int seconds, double? value) => new(Now.AddSeconds(seconds), value);
}
