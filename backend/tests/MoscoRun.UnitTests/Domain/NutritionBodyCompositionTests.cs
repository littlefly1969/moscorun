using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class NutritionBodyCompositionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Energy_components_do_not_double_count_gross_exercise()
    {
        EnergyAccounting.NetExerciseEnergy(500, 1440, 60).Should().Be(440);
        EnergyAccounting.TotalExpenditure(1440, 150, 250, 440).Should().Be(2280);
        EnergyAccounting.TotalExpenditure(1440, null, 250, 440).Should().BeNull();
        EnergyAccounting.EnergyBalance(2500, 2280).Should().Be(220);
    }

    [Fact]
    public void Energy_availability_requires_explicit_inputs()
    {
        EnergyAccounting.EnergyAvailability(2500, 500, 50).Should().Be(40);
        EnergyAccounting.EnergyAvailability(2500, 500, null).Should().BeNull();
        EnergyAccounting.EnergyAvailability(2500, -1, 50).Should().BeNull();
    }

    [Fact]
    public void Fueling_preserves_missing_nutrients_and_availability()
    {
        NutritionEvent[] events = [new(Now, Now, 30, 500, null),
            new(Now.AddMinutes(30), Now.AddHours(2), 100, 500, 100)];
        var result = FuelingEngine.Summarize(events, Now, Now.AddHours(1), Now.AddHours(1), diaryComplete: true);
        result.Should().NotBeNull();
        result!.EventCount.Should().Be(1);
        result.CarbohydrateGramsPerHour.Should().Be(30);
        result.FluidMillilitersPerHour.Should().Be(500);
        result.SodiumMilligramsPerHour.Should().BeNull();
        FuelingEngine.Summarize(events, Now, Now.AddHours(1), Now.AddHours(1), diaryComplete: false).Should().BeNull();
    }

    [Fact]
    public void Sweat_and_mass_calculations_use_explicit_units()
    {
        FuelingEngine.EstimatedSweatLitersPerHour(70, 69, 0.5, 0.1, 2)!.Value.Should().BeApproximately(0.7, 0.000001);
        BodyCompositionEngine.FatFreeMassKg(80, 25).Should().Be(60);
        BodyCompositionEngine.FatFreeMassKg(80, 100).Should().BeNull();
        BodyCompositionEngine.MedianWeight([70, 70.5, 90, 71, 70.7]).Should().Be(70.7);
    }
}
