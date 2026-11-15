using System.Globalization;
using MoscoRun.Domain.Common;

namespace MoscoRun.Domain.Workouts;

/// <summary>Cerchio intorno a un luogo privato (es. casa): partenza e arrivo al suo interno non vengono mostrati.</summary>
/// <param name="Latitude">Latitudine del centro.</param>
/// <param name="Longitude">Longitudine del centro.</param>
/// <param name="RadiusM">Raggio in metri.</param>
public sealed record PrivacyZone(double Latitude, double Longitude, double RadiusM)
{
    public const double MinRadiusM = 50;
    public const double MaxRadiusM = 5000;
    private const double EarthRadiusM = 6_371_008.8;

    public bool Contains(TrackPoint p)
    {
        // Haversine: esatta anche ai poli e attraverso l'antimeridiano.
        var dLat = (p.Latitude - Latitude) * Math.PI / 180;
        var dLon = (p.Longitude - Longitude) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + (Math.Cos(Latitude * Math.PI / 180) * Math.Cos(p.Latitude * Math.PI / 180) * Math.Pow(Math.Sin(dLon / 2), 2));
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1, Math.Sqrt(a))) <= RadiusM;
    }

    /// <summary>
    /// Legge l'elenco dalla configurazione: <c>lat,lon,raggio</c> separati da <c>;</c>, punto come separatore decimale.
    /// Una stringa vuota significa nessuna zona.
    /// </summary>
    public static Result<IReadOnlyList<PrivacyZone>> ParseList(string? value)
    {
        var zones = new List<PrivacyZone>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<IReadOnlyList<PrivacyZone>>.Success(zones);
        }

        foreach (var (item, index) in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select((s, i) => (s, i)))
        {
            var parts = item.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var radius)
                || lat is < -90 or > 90 || lon is < -180 or > 180 || radius is < MinRadiusM or > MaxRadiusM)
            {
                return Result<IReadOnlyList<PrivacyZone>>.Failure([new DomainError(
                    "privacy.zone.invalid",
                    $"Zona privata {index + 1} non valida: usare latitudine,longitudine,raggio con raggio tra {MinRadiusM} e {MaxRadiusM} m.",
                    "RouteZones")]);
            }

            zones.Add(new PrivacyZone(lat, lon, radius));
        }

        return Result<IReadOnlyList<PrivacyZone>>.Success(zones);
    }
}
