using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain;

public sealed class WorkoutEnvironmentEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly WorkoutEnvironmentProtocol Protocol = new(25, 60, 30, 10, 5, true, true);

    [Fact]
    public void Weather_and_route_are_paired_only_on_supported_intervals()
    {
        var track = new TrackPoint[] { new(Start, 0, 0, 0, 1), new(Start.AddSeconds(10), 0.0003, 0, 3, 1) };
        var channels = new[]
        {
            Channel("airTemperature", (0, 30), (10, 30)),
            Channel("humidity", (5, 50), (10, 50)),
            Channel("windSpeed", (0, 3), (10, 3)),
            Channel("windDirection", (0, 0), (10, 0)),
        };
        var report = WorkoutEnvironmentEngine.Build(Start, Start.AddSeconds(10), track, channels, Protocol)!;
        var vpd = report.Metrics.Single(item => string.Equals(item.Metric, "vaporPressureDeficit", StringComparison.Ordinal));
        vpd.SupportedSeconds.Should().Be(5);
        vpd.WeightedMean.Should().BeApproximately(EnvironmentalPhysiology.VaporPressureDeficitHpa(30, 50)!.Value, 1e-9);
        report.Metrics.Single(item => string.Equals(item.Metric, "headwind", StringComparison.Ordinal))
            .WeightedMean.Should().BeApproximately(3, 1e-9);
        report.HeatExposure!.DegreeMinutes.Should().BeApproximately(5.0 / 6, 1e-9);
        report.Terrain.RawAscentMeters.Should().Be(3);
        report.Terrain.NetGradePercent.Should().BeApproximately(300 / report.Terrain.HorizontalDistanceMeters, 1e-9);
    }

    [Fact]
    public void Missing_accuracy_or_unconfirmed_weather_never_becomes_valid_support()
    {
        var track = new TrackPoint[] { new(Start, 0, 0, 0), new(Start.AddSeconds(10), 0.0003, 0, 3) };
        var channels = new[] { Channel("airTemperature", (0, 30), (10, 30)) };
        var report = WorkoutEnvironmentEngine.Build(Start, Start.AddSeconds(10), track, channels,
            Protocol with { WeatherRepresentative = false })!;
        report.Terrain.NetGradePercent.Should().BeNull();
        report.HeatExposure.Should().BeNull();
        report.Metrics.Should().OnlyContain(item => item.WeightedMean == null);
        var gap = WorkoutEnvironmentEngine.Build(Start, Start.AddSeconds(10), track, channels,
            Protocol with { MaximumWeatherGapSeconds = 5 })!;
        gap.HeatExposure.Should().BeNull();
    }

    private static WeatherChannel Channel(string metric, params (int Offset, double Value)[] points) =>
        new(metric, points.Select(point => new SignalSample(Start.AddSeconds(point.Offset), point.Value)).ToArray());
}
