using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class CalibratedTwinEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly WorkoutCardiacRecoveryProtocol Protocol = new(30, "passive-seated", true, 70);
    private static readonly CalibratedTwinRequest Request = new(Protocol, 100, 50, 190, 3, true,
        [new(30, 150, 4), new(30, 80, 0)]);

    [Fact]
    public void Simulates_using_observed_recovery_tau_not_a_default_parameter()
    {
        var calibration = Calibration(new CardiacResponseFit(30, 1, 24));
        var report = CalibratedTwinEngine.Build(Request, calibration, Now.AddDays(-1), Now)!;
        report.Status.Should().Be("conditional-simulation");
        report.Parameters!.HeartRateTimeConstantSeconds.Should().Be(30);
        report.Segments[0].EndHeartRateBpm.Should().BeApproximately(150 - 50 * Math.Exp(-1), 1e-9);
        report.Segments[^1].CumulativeMeters.Should().Be(120);
    }

    [Fact]
    public void Abstains_on_missing_fit_unconfirmed_transfer_or_boundary_estimate()
    {
        var calibration = Calibration(new CardiacResponseFit(30, 1, 24));
        CalibratedTwinEngine.Build(Request with { TransferAssumptionConfirmed = false }, calibration, Now, Now)!
            .Segments.Should().BeEmpty();
        CalibratedTwinEngine.Build(Request, Calibration(null), Now, Now)!.Status.Should().Be("insufficient-calibration");
        CalibratedTwinEngine.Build(Request, Calibration(new CardiacResponseFit(300, 1, 24)), Now, Now)!
            .Segments.Should().BeEmpty();
        CalibratedTwinEngine.Build(Request, Calibration(new CardiacResponseFit(30, 4, 24)), Now, Now)!
            .Segments.Should().BeEmpty();
        CalibratedTwinEngine.Build(Request, calibration, Now.AddDays(1), Now).Should().BeNull();
    }

    private static WorkoutCardiacRecoveryReport Calibration(CardiacResponseFit? fit) =>
        new(Guid.NewGuid(), 1, "hash", "test", Now, WorkoutCardiacRecoveryEngine.MethodVersion, 5,
            Protocol, "observed", [], fit, fit is null ? "insufficient-fit-support" : "descriptive-fit");
}
