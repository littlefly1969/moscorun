using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class WearableIngestCatalogTests
{
    [Theory]
    [InlineData("hrvRmssd", "ms")]
    [InlineData("restingHeartRate", "bpm")]
    [InlineData("sleepDuration", "s")]
    [InlineData("sleepInBedDuration", "s")]
    [InlineData("spo2", "%")]
    [InlineData("respirationRate", "1/min")]
    [InlineData("vo2max", "mL/kg/min")]
    public void Accepts_wearable_observations_in_canonical_units(string metric, string unit) =>
        WearableIngestCatalog.AcceptsObservation(metric, unit).Should().BeTrue();

    [Theory]
    [InlineData("hrvRmssd", "s")]
    [InlineData("rpe", "0-10")]
    [InlineData("sleepQuality", "0-10")]
    [InlineData("nutritionDiaryComplete", "1")]
    [InlineData("rrInterval", "ms")]
    [InlineData("unknownMetric", "native")]
    [InlineData(null, "ms")]
    [InlineData("hrvRmssd", null)]
    public void Rejects_subjective_attested_signal_only_unknown_or_non_canonical_observations(string? metric, string? unit) =>
        WearableIngestCatalog.AcceptsObservation(metric, unit).Should().BeFalse();

    [Theory]
    [InlineData("rrInterval", "ms", true)]
    [InlineData("ppgGreen", "native", true)]
    [InlineData("ecgLeadOff", "native", true)]
    [InlineData("mfBiaMagnitude50k", "native", true)]
    [InlineData("groundContactTime", "ms", true)]
    [InlineData("verticalOscillation", "m", true)]
    [InlineData("airTemperature", "Cel", true)]
    [InlineData("speed", "m/s", true)]
    [InlineData("speed", "km/h", false)]
    [InlineData("sleepDuration", "s", false)]
    [InlineData("vo2max", "mL/kg/min", false)]
    public void Signals_are_limited_to_time_series_in_canonical_units(string metric, string unit, bool expected) =>
        WearableIngestCatalog.AcceptsSignal(metric, unit).Should().Be(expected);

    [Fact]
    public void Every_listed_metric_belongs_to_the_scientific_catalog()
    {
        var metrics = typeof(WearableIngestCatalog)
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(field => field.Name.EndsWith("Metrics", StringComparison.Ordinal))
            .SelectMany(field => (HashSet<string>)field.GetValue(null)!)
            .ToArray();

        metrics.Should().NotBeEmpty();
        metrics.Should().OnlyContain(metric => ScientificMetricCatalog.IsKnown(metric));
    }

    [Theory]
    [InlineData(ObservationMethod.Measured, true)]
    [InlineData(ObservationMethod.ProviderReported, true)]
    [InlineData(ObservationMethod.ProviderEstimated, true)]
    [InlineData(ObservationMethod.SelfReported, false)]
    [InlineData(ObservationMethod.Derived, false)]
    [InlineData(ObservationMethod.ModelInferred, false)]
    public void Only_measurements_and_provider_values_come_from_wearables(ObservationMethod method, bool expected) =>
        WearableIngestCatalog.AcceptsMethod(method).Should().Be(expected);

    [Theory]
    [InlineData("health-connect", true)]
    [InlineData("samsung-health", true)]
    [InlineData("garmin-connect", true)]
    [InlineData("samsung-sensor", true)]
    [InlineData("Health-Connect", false)]
    [InlineData("fitbit", false)]
    [InlineData(null, false)]
    public void Sources_are_the_supported_connectors(string? source, bool expected) =>
        WearableIngestCatalog.IsSource(source).Should().Be(expected);
}
