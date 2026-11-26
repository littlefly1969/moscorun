using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

/// <summary>Esposizione descrittiva, senza correzione di prestazione o classificazione di sicurezza termica.</summary>
public static class WorkoutEnvironmentEngine
{
    public const string MethodVersion = "environment-native-paired-support-v1";
    public const double MaximumRunningSpeedMetersPerSecond = 15;

    public static bool Supported(string metric) => metric is "airTemperature" or "humidity" or "windSpeed" or "windDirection";

    public static WorkoutEnvironmentAnalysis? Build(DateTimeOffset start, DateTimeOffset end, IReadOnlyList<TrackPoint> track,
        IReadOnlyList<WeatherChannel> channels, WorkoutEnvironmentProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(protocol);
        if (end <= start || !protocol.IsValid() || channels.Count > 4 ||
            channels.Any(channel => !Supported(channel.Metric)) ||
            channels.Select(channel => channel.Metric).Distinct(StringComparer.Ordinal).Count() != channels.Count)
        {
            return null;
        }

        var path = Route(track, start, end, protocol);
        var support = protocol.WeatherRepresentative ? channels.ToDictionary(channel => channel.Metric,
            channel => Support(channel, start, end, protocol.MaximumWeatherGapSeconds), StringComparer.Ordinal) :
            new Dictionary<string, Slice[]>(StringComparer.Ordinal);
        var temperature = support.GetValueOrDefault("airTemperature") ?? [];
        var humidity = support.GetValueOrDefault("humidity") ?? [];
        var wind = support.GetValueOrDefault("windSpeed") ?? [];
        var direction = protocol.WindDirectionIsFrom ? support.GetValueOrDefault("windDirection") ?? [] : [];
        var vpd = Combine(temperature, humidity, EnvironmentalPhysiology.VaporPressureDeficitHpa);
        var headwind = Headwind(wind, direction, path);
        var duration = (end - start).TotalSeconds;
        var heat = EnvironmentalPhysiology.SummarizeHeatExposure(temperature.Select(item => item.Value).ToArray(),
            temperature.Select(item => item.Seconds / 60).ToArray(), protocol.HeatThresholdCelsius);
        return new WorkoutEnvironmentAnalysis(MethodVersion, duration, Array.AsReadOnly(new[]
        {
            Summary("airTemperature", "Cel", temperature, duration),
            Summary("humidity", "%", humidity, duration),
            Summary("vaporPressureDeficit", "hPa", vpd, duration),
            Summary("windSpeed", "m/s", wind, duration),
            Summary("headwind", "m/s", headwind, duration),
        }), heat, Terrain(path));
    }

    private static Slice[] Support(WeatherChannel channel, DateTimeOffset start, DateTimeOffset end, double maxGap)
    {
        var slots = channel.Samples.GroupBy(item => item.Time).Select(SignalProcessing.Resolve)
            .OrderBy(item => item.Time).ToArray();
        var result = new List<Slice>();
        for (var index = 1; index < slots.Length; index++)
        {
            var a = slots[index - 1];
            var b = slots[index];
            if ((b.Time - a.Time).TotalSeconds > maxGap || !Valid(channel.Metric, a.Value) || !Valid(channel.Metric, b.Value))
            {
                continue;
            }

            var from = a.Time > start ? a.Time : start;
            var to = b.Time < end ? b.Time : end;
            if (to > from)
            {
                result.Add(new Slice(from, to, a.Value.GetValueOrDefault()));
            }
        }

        return result.ToArray();
    }

    private static bool Valid(string metric, double? value) => value is { } number && double.IsFinite(number) &&
        (metric switch
        {
            "airTemperature" => number is >= -40 and <= 60,
            "humidity" => number is >= 0 and <= 100,
            "windDirection" => number is >= 0 and < 360,
            _ => number >= 0,
        });

    private static Slice[] Combine(Slice[] first, Slice[] second, Func<double, double, double?> derive)
    {
        var result = new List<Slice>();
        var i = 0;
        var j = 0;
        while (i < first.Length && j < second.Length)
        {
            var a = first[i];
            var b = second[j];
            var start = a.Start > b.Start ? a.Start : b.Start;
            var end = a.End < b.End ? a.End : b.End;
            if (end > start && derive(a.Value, b.Value) is { } value && double.IsFinite(value))
            {
                result.Add(new Slice(start, end, value));
            }

            if (a.End <= b.End)
            {
                i++;
            }

            if (b.End <= a.End)
            {
                j++;
            }
        }

        return result.ToArray();
    }

    private static Slice[] Headwind(Slice[] wind, Slice[] direction, RouteSlice[] path)
    {
        var result = new List<Slice>();
        var i = 0;
        var j = 0;
        var k = 0;
        while (i < wind.Length && j < direction.Length && k < path.Length)
        {
            var a = wind[i];
            var b = direction[j];
            var c = path[k];
            var start = new[] { a.Start, b.Start, c.Start }.Max();
            var end = new[] { a.End, b.End, c.End }.Min();
            if (end > start && EnvironmentalContext.HeadwindMetersPerSecond(a.Value, b.Value, c.Bearing) is { } value)
            {
                result.Add(new Slice(start, end, value));
            }

            if (a.End == end)
            {
                i++;
            }

            if (b.End == end)
            {
                j++;
            }

            if (c.End == end)
            {
                k++;
            }
        }

        return result.ToArray();
    }

    private static RouteSlice[] Route(IReadOnlyList<TrackPoint> track, DateTimeOffset start, DateTimeOffset end,
        WorkoutEnvironmentProtocol protocol)
    {
        var slots = track.Where(point => point.Time >= start && point.Time <= end).GroupBy(point => point.Time)
            .OrderBy(group => group.Key).Select(group => group.Distinct().ToArray()).ToArray();
        var path = new List<RouteSlice>();
        for (var index = 1; index < slots.Length; index++)
        {
            if (slots[index - 1].Length != 1 || slots[index].Length != 1)
            {
                continue;
            }

            var slice = RouteSegment(slots[index - 1][0], slots[index][0], protocol);
            if (slice is not null)
            {
                path.Add(slice);
            }
        }

        return path.ToArray();
    }

    private static RouteSlice? RouteSegment(TrackPoint a, TrackPoint b, WorkoutEnvironmentProtocol protocol)
    {
        var seconds = (b.Time - a.Time).TotalSeconds;
        if (seconds <= 0 || seconds > protocol.MaximumGpsGapSeconds ||
            a.AccuracyM is not { } accuracyA || b.AccuracyM is not { } accuracyB ||
            !double.IsFinite(accuracyA) || !double.IsFinite(accuracyB) || accuracyA < 0 || accuracyB < 0 ||
            accuracyA > protocol.MaximumHorizontalAccuracyMeters || accuracyB > protocol.MaximumHorizontalAccuracyMeters)
        {
            return null;
        }

        var distance = EnvironmentalContext.HorizontalDistanceMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
        if (distance is not { } meters || meters < protocol.MinimumHorizontalDistanceMeters ||
            meters <= accuracyA + accuracyB || meters / seconds > MaximumRunningSpeedMetersPerSecond)
        {
            return null;
        }

        var elevation = a.AltitudeM is { } low && b.AltitudeM is { } high && double.IsFinite(low) && double.IsFinite(high)
            ? high - low : (double?)null;
        return new RouteSlice(a.Time, b.Time, meters, Bearing(a, b), elevation);
    }

    private static double Bearing(TrackPoint a, TrackPoint b)
    {
        var lat1 = a.Latitude * Math.PI / 180;
        var lat2 = b.Latitude * Math.PI / 180;
        var delta = (b.Longitude - a.Longitude) * Math.PI / 180;
        var angle = Math.Atan2(Math.Sin(delta) * Math.Cos(lat2),
            Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(delta));
        return (angle * 180 / Math.PI + 360) % 360;
    }

    private static TerrainSummary Terrain(RouteSlice[] route)
    {
        var valid = route.Where(item => item.ElevationChange is { } change && double.IsFinite(change)).ToArray();
        var distance = valid.Sum(item => item.Distance);
        var up = valid.Sum(item => Math.Max(0, item.ElevationChange.GetValueOrDefault()));
        var down = valid.Sum(item => Math.Max(0, -item.ElevationChange.GetValueOrDefault()));
        return new TerrainSummary(valid.Sum(item => (item.End - item.Start).TotalSeconds), distance,
            EnvironmentalPhysiology.GradePercent(up - down, distance), valid.Length == 0 ? null : up, valid.Length == 0 ? null : down);
    }

    private static EnvironmentalMetricSummary Summary(string metric, string unit, Slice[] slices, double duration)
    {
        var seconds = slices.Sum(item => item.Seconds);
        var mean = seconds > 0 ? slices.Sum(item => item.Seconds * item.Value) / seconds : double.NaN;
        return new EnvironmentalMetricSummary(metric, unit, seconds, seconds / duration, double.IsFinite(mean) ? mean : null);
    }

    private sealed record Slice(DateTimeOffset Start, DateTimeOffset End, double Value)
    {
        public double Seconds => (End - Start).TotalSeconds;
    }

    private sealed record RouteSlice(DateTimeOffset Start, DateTimeOffset End, double Distance, double Bearing, double? ElevationChange);
}
