namespace MoscoRun.Domain.Analysis;

public static class EnvironmentalContext
{
    /// <summary>Componente di vento contrario positiva; direzione meteorologica da cui proviene il vento.</summary>
    public static double? HeadwindMetersPerSecond(double windSpeed, double windFromDegrees, double travelBearingDegrees)
    {
        if (!double.IsFinite(windSpeed) || windSpeed < 0 || !ValidAngle(windFromDegrees) || !ValidAngle(travelBearingDegrees))
        {
            return null;
        }

        return windSpeed * Math.Cos((windFromDegrees - travelBearingDegrees) * Math.PI / 180);
    }

    /// <summary>Distanza orizzontale sferica Haversine; non corregge l'errore del GPS.</summary>
    public static double? HorizontalDistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        if (!ValidCoordinate(latitude1, longitude1) || !ValidCoordinate(latitude2, longitude2))
        {
            return null;
        }

        var lat1 = latitude1 * Math.PI / 180;
        var lat2 = latitude2 * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = (longitude2 - longitude1) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * 6371008.8 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    private static bool ValidAngle(double value) => double.IsFinite(value) && value >= 0 && value < 360;

    private static bool ValidCoordinate(double latitude, double longitude) =>
        double.IsFinite(latitude) && latitude >= -90 && latitude <= 90 &&
        double.IsFinite(longitude) && longitude >= -180 && longitude <= 180;
}
