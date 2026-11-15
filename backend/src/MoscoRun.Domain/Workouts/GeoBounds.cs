namespace MoscoRun.Domain.Workouts;

/// <summary>Riquadro che contiene il tracciato (gradi WGS84).</summary>
public sealed record GeoBounds(double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude)
{
    public static GeoBounds? Of(IReadOnlyList<TrackPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return null;
        }

        double minLat = double.MaxValue, minLon = double.MaxValue, maxLat = double.MinValue, maxLon = double.MinValue;
        foreach (var p in points)
        {
            minLat = Math.Min(minLat, p.Latitude);
            maxLat = Math.Max(maxLat, p.Latitude);
            minLon = Math.Min(minLon, p.Longitude);
            maxLon = Math.Max(maxLon, p.Longitude);
        }

        return new GeoBounds(minLat, minLon, maxLat, maxLon);
    }
}
