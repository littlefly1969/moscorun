using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class ScientificObservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Missing_value_is_not_zero()
    {
        var observation = NewObservation(null);
        observation.Value.Should().BeNull();
        observation.IsMapped.Should().BeTrue();
    }

    [Fact]
    public void Unknown_metric_is_preserved_without_mapping()
    {
        var observation = new ScientificObservation(Guid.NewGuid(), Owner, "vendorMetric", "native", 4, Now, Now,
            "garmin", ObservationMethod.ProviderReported);
        observation.IsMapped.Should().BeFalse();
        observation.Value.Should().Be(4);
    }

    [Fact]
    public void Incorrect_unit_is_rejected()
    {
        var action = () => new ScientificObservation(Guid.NewGuid(), Owner, "heartRate", "m/s", 140, Now, Now,
            "samsung", ObservationMethod.Measured);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Non_finite_value_is_rejected()
    {
        var action = () => NewObservation(double.NaN);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Provider_estimate_requires_algorithm_metadata()
    {
        var action = () => new ScientificObservation(Guid.NewGuid(), Owner, "vo2max", "mL/kg/min", 50, Now, Now,
            "garmin", ObservationMethod.ProviderEstimated);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Derived_observation_requires_lineage()
    {
        var action = () => new ScientificObservation(Guid.NewGuid(), Owner, "heartRate", "bpm", 140, Now, Now,
            "moscorun", ObservationMethod.Derived);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Replay_excludes_future_availability_and_missing_lineage()
    {
        var raw = NewObservation(140, Now.AddMinutes(1));
        var derived = new ScientificObservation(Guid.NewGuid(), Owner, "heartRate", "bpm", 140, Now, Now.AddMinutes(2),
            "moscorun", ObservationMethod.Derived, derivationVersion: "1", inputIds: [raw.Id]);

        ScientificSnapshot.Select(Owner, Now, [derived, raw]).Should().BeEmpty();
        ScientificSnapshot.Select(Owner, Now.AddMinutes(2), [derived]).Should().BeEmpty();
        ScientificSnapshot.Select(Owner, Now.AddMinutes(2), [derived, raw]).Should().Equal(raw, derived);
    }

    [Fact]
    public void Replay_rejects_cross_owner_dataset()
    {
        var action = () => ScientificSnapshot.Select(Guid.NewGuid(), Now, [NewObservation(140)]);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Replay_rejects_duplicate_identity()
    {
        var observation = NewObservation(140);
        var action = () => ScientificSnapshot.Select(Owner, Now, [observation, observation]);
        action.Should().Throw<ArgumentException>();
    }

    private static ScientificObservation NewObservation(double? value, DateTimeOffset? availableAt = null) =>
        new(Guid.NewGuid(), Owner, "heartRate", "bpm", value, Now, availableAt ?? Now, "samsung", ObservationMethod.Measured);
}
