using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class TrainingLoadEngineTests
{
    [Fact]
    public void Session_rpe_uses_minutes_and_rating()
    {
        TrainingLoadEngine.SessionRpe(60, 5).Should().Be(300);
        TrainingLoadEngine.SessionRpe(60, null).Should().BeNull();
        TrainingLoadEngine.SessionRpe(60, 11).Should().BeNull();
    }

    [Fact]
    public void Heart_rate_reserve_load_is_not_extrapolated()
    {
        TrainingLoadEngine.HeartRateReserveLoad(60, 140, 60, 180).Should().Be(40);
        TrainingLoadEngine.HeartRateReserveLoad(60, 181, 60, 180).Should().BeNull();
    }

    [Fact]
    public void Distance_increase_uses_previous_longest()
    {
        TrainingLoadEngine.DistanceIncreasePercent(11000, 10000)!.Value.Should().BeApproximately(10, 0.000001);
        TrainingLoadEngine.DistanceIncreasePercent(10000, null).Should().BeNull();
    }

    [Fact]
    public void Weekly_summary_distinguishes_rest_and_missing_data()
    {
        var result = TrainingLoadEngine.SummarizeWeek([100, 0, 100, 0, 100, 0, 100]);
        result.Should().NotBeNull();
        result!.TotalLoad.Should().Be(400);
        result.Monotony.Should().NotBeNull();
        TrainingLoadEngine.SummarizeWeek([100, 0, 100, null, 100, 0, 100]).Should().BeNull();
    }

    [Fact]
    public void Constant_load_has_undefined_monotony()
    {
        var result = TrainingLoadEngine.SummarizeWeek([100, 100, 100, 100, 100, 100, 100]);
        result.Should().NotBeNull();
        result!.Monotony.Should().BeNull();
        result.Strain.Should().BeNull();
    }
}
