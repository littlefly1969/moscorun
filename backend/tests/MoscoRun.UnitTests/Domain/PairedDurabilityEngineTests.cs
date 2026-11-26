using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class PairedDurabilityEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Session = Guid.NewGuid();

    [Fact]
    public void Intersects_asynchronous_support_and_weights_both_channels_identically()
    {
        SignalSample[] speed = [At(0, 4), At(4, 5), At(8, 6), At(10, 6)];
        SignalSample[] heart = [At(2, 140), At(6, 160), At(10, 170)];
        var result = PairedDurabilityEngine.BuildWindow(Session, ExternalLoadMetric.Speed, speed, heart,
            Now, Now.AddSeconds(10), TimeSpan.FromSeconds(3));
        result.Should().NotBeNull();
        var window = result!.Value;
        window.PairedDuration.Should().Be(TimeSpan.FromSeconds(4));
        window.MeanExternalLoad.Should().BeApproximately(5, 0.000001);
        window.MeanHeartRateBpm.Should().BeApproximately(150, 0.000001);
    }

    [Fact]
    public void Missing_and_conflicting_samples_do_not_bridge_gaps()
    {
        SignalSample[] speed = [At(0, 4), At(2, null), At(4, 4), At(6, 4), At(6, 5), At(8, 4)];
        SignalSample[] heart = [At(0, 140), At(2, 140), At(4, 140), At(6, 140), At(8, 140)];
        PairedDurabilityEngine.BuildWindow(Session, ExternalLoadMetric.Speed, speed, heart,
            Now, Now.AddSeconds(8), TimeSpan.FromSeconds(3)).Should().BeNull();
    }

    [Fact]
    public void Excludes_pauses_and_does_not_use_a_future_sample_to_extend_support()
    {
        SignalSample[] speed = [At(0, 4), At(2, 4), At(4, 0), At(6, 4), At(10, 4)];
        SignalSample[] heart = [At(0, 150), At(2, 150), At(4, 150), At(6, 150), At(8, 150)];
        var result = PairedDurabilityEngine.BuildWindow(Session, ExternalLoadMetric.Speed, speed, heart,
            Now, Now.AddSeconds(8), TimeSpan.FromSeconds(3));
        result.Should().NotBeNull();
        result!.Value.PairedDuration.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Paired_windows_feed_the_existing_durability_engine()
    {
        SignalSample[] power = [At(0, 250), At(5, 250), At(10, 225), At(15, 225), At(20, 225)];
        SignalSample[] heart = [At(0, 150), At(5, 150), At(10, 150), At(15, 150), At(20, 150)];
        var early = PairedDurabilityEngine.BuildWindow(Session, ExternalLoadMetric.Power, power, heart,
            Now, Now.AddSeconds(10), TimeSpan.FromSeconds(5));
        var late = PairedDurabilityEngine.BuildWindow(Session, ExternalLoadMetric.Power, power, heart,
            Now.AddSeconds(10), Now.AddSeconds(20), TimeSpan.FromSeconds(5));
        early.Should().NotBeNull();
        late.Should().NotBeNull();
        var comparison = DurabilityEngine.Compare(early!.Value, late!.Value, 0.8);
        comparison.Should().NotBeNull();
        comparison!.EfficiencyLossPercent.Should().BeApproximately(10, 0.000001);
    }

    private static SignalSample At(int seconds, double? value) => new(Now.AddSeconds(seconds), value);
}
