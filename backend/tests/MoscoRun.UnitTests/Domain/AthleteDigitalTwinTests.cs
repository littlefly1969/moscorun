using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class AthleteDigitalTwinTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TwinParameters Parameters = new("synthetic-v1", Now, 30, 50, 190);

    [Fact]
    public void Simulation_integrates_distance_and_cardiac_response()
    {
        var result = AthleteDigitalTwin.Simulate([new(30, 160, 4), new(30, 100, 0)], 100, Parameters, Now);
        result.Should().NotBeNull();
        result![0].EndHeartRateBpm.Should().BeApproximately(160 - 60 * Math.Exp(-1), 0.000001);
        result[0].MeanHeartRateBpm.Should().BeApproximately(160 - 60 * (1 - Math.Exp(-1)), 0.000001);
        result[1].CumulativeMeters.Should().Be(120);
        result[1].CumulativeSeconds.Should().Be(60);
        result[1].EndHeartRateBpm.Should().BeLessThan(result[0].EndHeartRateBpm);
    }

    [Fact]
    public void Future_calibration_and_invalid_equilibrium_are_rejected()
    {
        AthleteDigitalTwin.Simulate([new(30, 160, 4)], 100, Parameters, Now.AddDays(-1)).Should().BeNull();
        AthleteDigitalTwin.Simulate([new(30, 200, 4)], 100, Parameters, Now).Should().BeNull();
    }

    [Fact]
    public void Scenarios_must_share_horizon()
    {
        var baseline = AthleteDigitalTwin.Simulate([new(60, 150, 3)], 100, Parameters, Now)!;
        var alternative = AthleteDigitalTwin.Simulate([new(60, 160, 4)], 100, Parameters, Now)!;
        var result = TwinScenarioEngine.Compare(baseline, alternative);
        result.Should().NotBeNull();
        result!.DistanceDifferenceMeters.Should().Be(60);
        result.EndHeartRateDifferenceBpm.Should().BePositive();
        TwinScenarioEngine.Compare(baseline, [new(30, 120, 150, 130)]).Should().BeNull();
    }
}
