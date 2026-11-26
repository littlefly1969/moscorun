using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class NutritionFeedbackEngineTests
{
    private static readonly DateTimeOffset End = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Complete_session_totals_use_elapsed_time_and_controlled_mass_balance()
    {
        var inputs = new Dictionary<string, double?>(StringComparer.Ordinal)
        {
            [NutritionFeedbackMetrics.Complete] = 1,
            ["carbohydrateIntake"] = 90,
            ["fluidIntake"] = 600,
            ["sodiumIntake"] = 0,
            [NutritionFeedbackMetrics.SweatProtocol] = 1,
            [NutritionFeedbackMetrics.BeforeWeight] = 70,
            [NutritionFeedbackMetrics.AfterWeight] = 69.5,
            [NutritionFeedbackMetrics.Urine] = 100,
        };
        var report = NutritionFeedbackEngine.Build(inputs, true, 1.5, End, End);
        report.CarbohydrateGramsPerHour.Should().Be(60);
        report.FluidMillilitersPerHour.Should().Be(400);
        report.SodiumMilligramsPerHour.Should().Be(0);
        report.EstimatedSweatLitersPerHour.Should().BeApproximately(1d / 1.5, 1e-10);
        inputs[NutritionFeedbackMetrics.Urine] = null;
        NutritionFeedbackEngine.Build(inputs, true, 1.5, End, End).EstimatedSweatLitersPerHour.Should().BeNull();
        NutritionFeedbackEngine.Build(inputs, true, 1.5, End.AddSeconds(-1), End).FluidMillilitersPerHour.Should().BeNull();
    }

    [Fact]
    public void Unknown_energy_components_are_not_zero_and_availability_has_independent_inputs()
    {
        var inputs = new Dictionary<string, double?>(StringComparer.Ordinal)
        {
            [NutritionFeedbackMetrics.Complete] = 1,
            ["energyIntake"] = 2400,
            ["restingEnergyExpenditure"] = 1500,
            [NutritionFeedbackMetrics.ExerciseNet] = 600,
            ["fatFreeMass"] = 60,
        };
        var report = NutritionFeedbackEngine.Build(inputs, false, null, End, End);
        report.TotalExpenditureKcal.Should().BeNull();
        report.EnergyBalanceKcal.Should().BeNull();
        report.EnergyAvailabilityKcalPerKg.Should().Be(30);
        report.Status.Should().Be("partial-inputs");
        inputs[NutritionFeedbackMetrics.Complete] = 0;
        NutritionFeedbackEngine.Build(inputs, false, null, End, End).EnergyAvailabilityKcalPerKg.Should().BeNull();
    }
}
