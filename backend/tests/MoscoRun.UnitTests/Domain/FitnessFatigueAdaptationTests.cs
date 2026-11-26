using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class FitnessFatigueAdaptationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 11, 25);

    [Fact]
    public void Rest_decays_fatigue_faster_than_fitness()
    {
        var parameters = new FitnessFatigueParameters(40, 5, 1, 2);
        var state = new FitnessFatigueState(Day.AddDays(-1), 100, 100);
        var result = FitnessFatigueEngine.Replay([new(Day, Now, 0)], Day, Day, Now, parameters, state);
        result.Should().NotBeNull();
        result!.Fitness.Should().BeApproximately(100 * Math.Exp(-1.0 / 40), 0.000001);
        result.Fatigue.Should().BeApproximately(100 * Math.Exp(-1.0 / 5), 0.000001);
        result.NetResponse.Should().BePositive();
    }

    [Fact]
    public void Replay_rejects_missing_duplicate_or_late_load()
    {
        var parameters = new FitnessFatigueParameters(40, 5, 1, 2);
        var initial = new FitnessFatigueState(Day.AddDays(-1), 0, 0);
        FitnessFatigueEngine.Replay([new(Day, Now, null)], Day, Day, Now, parameters, initial).Should().BeNull();
        FitnessFatigueEngine.Replay([new(Day, Now.AddHours(1), 100)], Day, Day, Now, parameters, initial).Should().BeNull();
        FitnessFatigueEngine.Replay([new(Day, Now, 100), new(Day, Now, 100)], Day, Day, Now, parameters, initial)
            .Should().BeNull();
        FitnessFatigueEngine.Replay([new(Day, Now, 100)], Day, Day.AddDays(1), Now, parameters, initial).Should().BeNull();
    }

    [Fact]
    public void Observed_impulse_changes_both_states()
    {
        var result = FitnessFatigueEngine.Replay([new(Day, Now, 50)], Day, Day, Now,
            new FitnessFatigueParameters(40, 5, 1, 2), new FitnessFatigueState(Day.AddDays(-1), 0, 0));
        result.Should().NotBeNull();
        result!.Fitness.Should().Be(50);
        result.Fatigue.Should().Be(100);
        result.NetResponse.Should().Be(-50);
    }

    [Fact]
    public void Adaptation_evaluation_uses_only_available_holdout_outcomes()
    {
        AdaptationObservation[] observations = [new(Now, Now, 20, 0, Now.AddHours(-1)),
            new(Now.AddDays(1), Now.AddDays(1), 4, 3, Now),
            new(Now.AddDays(2), Now.AddDays(2), 3, 4, Now),
            new(Now.AddDays(3), Now.AddDays(3), 5, 5, Now),
            new(Now.AddDays(4), Now.AddDays(8), 200, 0, Now)];
        var result = AdaptationEngine.Evaluate(observations, Now, Now.AddDays(5));
        result.Should().NotBeNull();
        result!.Count.Should().Be(3);
        result.MeanError.Should().Be(0);
        result.MeanAbsoluteError.Should().BeApproximately(2.0 / 3, 0.000001);
        observations[1] = observations[1] with { IssuedAt = observations[1].Time };
        AdaptationEngine.Evaluate(observations, Now, Now.AddDays(5)).Should().BeNull();
    }
}
