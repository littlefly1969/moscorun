using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class RrChunkEngineTests
{
    [Fact]
    public void Consecutive_validated_beats_produce_resting_hrv_and_dfa()
    {
        var samples = Samples();
        var report = RrChunkEngine.Analyze(samples, "resting", true, true, ObservationMethod.Measured)!;
        report.Status.Should().Be("observed");
        report.Hrv!.IntervalCount.Should().Be(200);
        report.Hrv.RmssdMilliseconds.Should().BeApproximately(20, 1e-8);
        report.Dfa.Should().NotBeNull();
        report.IntervalDurationSeconds.Should().BeApproximately(160, 1e-8);
        var exercise = RrChunkEngine.Analyze(samples, "exercise", true, true, ObservationMethod.Measured)!;
        exercise.Hrv.Should().BeNull();
        exercise.Dfa.Should().NotBeNull();
    }

    [Fact]
    public void Rejects_dropped_beats_missing_values_and_unconfirmed_metadata()
    {
        var samples = Samples();
        var unconfirmed = RrChunkEngine.Analyze(samples, "resting", false, true, ObservationMethod.Measured)!;
        unconfirmed.Status.Should().Be("unconfirmed-protocol");
        unconfirmed.Hrv.Should().BeNull();
        samples[50] = samples[50] with { Time = samples[50].Time.AddMilliseconds(20) };
        var discontinuity = RrChunkEngine.Analyze(samples, "resting", true, true, ObservationMethod.Measured)!;
        discontinuity.Status.Should().Be("discontinuous-beats");
        discontinuity.Dfa.Should().BeNull();
        samples[50] = samples[50] with { Value = null };
        RrChunkEngine.Analyze(samples, "resting", true, true, ObservationMethod.Measured)!.Status
            .Should().Be("invalid-interval");
        RrChunkEngine.Analyze(Samples(), "resting", true, true, ObservationMethod.ProviderEstimated)!.Status
            .Should().Be("unsupported-method");
    }

    private static SignalSample[] Samples()
    {
        var time = DateTimeOffset.UnixEpoch;
        var result = new SignalSample[200];
        for (var index = 0; index < result.Length; index++)
        {
            var rr = index % 2 == 0 ? 790 : 810;
            time = time.AddMilliseconds(rr);
            result[index] = new SignalSample(time, rr);
        }

        return result;
    }
}
