using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

/// <summary>Tracciati sintetici riproducibili (seed fisso) per i test.</summary>
internal static class TestTracks
{
    public static readonly DateTimeOffset Start = new(2026, 11, 14, 7, 0, 0, TimeSpan.Zero);

    /// <summary>Corsa a 1 Hz con andamento sinuoso intorno a Roma, altitudine e precisione realistiche.</summary>
    public static TrackPoint[] Run(int seconds, int seed = 42)
    {
        var random = new Random(seed);
        var points = new TrackPoint[seconds];
        double lat = 41.8902, lon = 12.4922, alt = 21;
        for (var i = 0; i < seconds; i++)
        {
            lat += (Math.Sin(i / 120.0) * 2.5e-5) + ((random.NextDouble() - 0.5) * 2e-6);
            lon += (Math.Cos(i / 90.0) * 2.5e-5) + ((random.NextDouble() - 0.5) * 2e-6);
            alt += (random.NextDouble() - 0.5) * 0.4;
            points[i] = new TrackPoint(Start.AddSeconds(i), lat, lon, i % 7 == 0 ? null : alt, 3 + (random.NextDouble() * 5));
        }

        return points;
    }

    public static SeriesSample[] HeartRate(int seconds) =>
        [.. Enumerable.Range(0, seconds).Select(i => new SeriesSample(Start.AddSeconds(i), 120 + (30 * Math.Sin(i / 300.0))))];
}
