namespace MoscoRun.Domain.Workouts;

public static class RoutePrivacy
{
    /// <summary>
    /// Nasconde i punti iniziali e finali che cadono in una zona privata. I passaggi a metà percorso restano visibili:
    /// la zona protegge il luogo da cui si parte e a cui si torna, non impedisce di disegnare la corsa.
    /// Il dato salvato non cambia: la protezione vale per ciò che viene mostrato o condiviso.
    /// </summary>
    public static VisibleRoute Apply(IReadOnlyList<TrackPoint> points, IReadOnlyList<PrivacyZone> zones)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(zones);
        if (zones.Count == 0)
        {
            return new VisibleRoute(points, 0, 0);
        }

        bool Private(TrackPoint p) => zones.Any(z => z.Contains(p));

        var first = 0;
        while (first < points.Count && Private(points[first]))
        {
            first++;
        }

        var last = points.Count - 1;
        while (last >= first && Private(points[last]))
        {
            last--;
        }

        if (first > last)
        {
            return new VisibleRoute([], points.Count, 0);
        }

        return new VisibleRoute(points.Skip(first).Take(last - first + 1).ToArray(), first, points.Count - 1 - last);
    }
}
