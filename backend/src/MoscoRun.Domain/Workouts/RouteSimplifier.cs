namespace MoscoRun.Domain.Workouts;

/// <summary>
/// Riduce un tracciato a un numero massimo di punti conservandone la forma (Douglas-Peucker a classifica):
/// ogni punto riceve la distanza dal segmento che lo ha reso necessario, e si tengono i punti più significativi.
/// A differenza della soglia fissa in metri, il risultato ha esattamente il numero di punti richiesto,
/// quindi il carico per la mappa è prevedibile; ogni sottoinsieme ottenuto è anche un risultato
/// Douglas-Peucker valido (la significatività di un punto non supera mai quella del punto che lo ha generato).
/// </summary>
public static class RouteSimplifier
{
    private const double EarthRadiusM = 6_371_008.8;

    public static IReadOnlyList<TrackPoint> Simplify(IReadOnlyList<TrackPoint> points, int maxPoints)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 2);
        if (points.Count <= maxPoints)
        {
            return points;
        }

        var significance = Rank(points);
        var keep = Enumerable.Range(0, points.Count)
            .OrderByDescending(i => significance[i])
            .ThenBy(i => i)
            .Take(maxPoints)
            .Order()
            .Select(i => points[i])
            .ToArray();
        return keep;
    }

    /// <summary>Significatività di ogni punto in metri; gli estremi sono infiniti.</summary>
    internal static double[] Rank(IReadOnlyList<TrackPoint> points)
    {
        var n = points.Count;
        var significance = new double[n];
        if (n == 0)
        {
            return significance;
        }

        // Proiezione equirettangolare locale: errore trascurabile sulla scala di un allenamento.
        var referenceLat = points[0].Latitude * Math.PI / 180;
        var kx = Math.Cos(referenceLat) * EarthRadiusM * Math.PI / 180;
        const double ky = EarthRadiusM * Math.PI / 180;
        var x = new double[n];
        var y = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = points[i].Longitude * kx;
            y[i] = points[i].Latitude * ky;
        }

        significance[0] = double.PositiveInfinity;
        significance[n - 1] = double.PositiveInfinity;

        var stack = new Stack<(int First, int Last, double Bound)>();
        stack.Push((0, n - 1, double.PositiveInfinity));
        while (stack.Count > 0)
        {
            var (first, last, bound) = stack.Pop();
            if (last - first < 2)
            {
                continue;
            }

            var index = first + 1;
            var max = -1.0;
            for (var i = first + 1; i < last; i++)
            {
                var d = SegmentDistance(x[i], y[i], x[first], y[first], x[last], y[last]);
                if (d > max)
                {
                    (max, index) = (d, i);
                }
            }

            var value = Math.Min(max, bound);
            significance[index] = value;
            stack.Push((first, index, value));
            stack.Push((index, last, value));
        }

        return significance;
    }

    private static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared == 0 ? 0 : Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared, 0, 1);
        var cx = ax + (t * dx) - px;
        var cy = ay + (t * dy) - py;
        return Math.Sqrt((cx * cx) + (cy * cy));
    }
}
