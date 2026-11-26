namespace MoscoRun.Domain.Analysis;

/// <summary>Descrittori ambientali. Non correggono automaticamente la prestazione e non stimano la sicurezza termica.</summary>
public static class EnvironmentalPhysiology
{
    public static double? VaporPressureDeficitHpa(double airTemperatureCelsius, double relativeHumidityPercent)
    {
        if (!double.IsFinite(airTemperatureCelsius) || airTemperatureCelsius < -40 || airTemperatureCelsius > 60 ||
            !double.IsFinite(relativeHumidityPercent) || relativeHumidityPercent < 0 || relativeHumidityPercent > 100)
        {
            return null;
        }

        // Approssimazione Magnus, pressione di saturazione rispetto all'acqua liquida.
        var saturation = 6.112 * Math.Exp(17.62 * airTemperatureCelsius / (243.12 + airTemperatureCelsius));
        return saturation * (1 - relativeHumidityPercent / 100);
    }

    public static double? GradePercent(double elevationChangeMeters, double horizontalDistanceMeters)
    {
        if (!double.IsFinite(elevationChangeMeters) || !double.IsFinite(horizontalDistanceMeters) ||
            horizontalDistanceMeters <= 0)
        {
            return null;
        }

        var grade = 100 * (elevationChangeMeters / horizontalDistanceMeters);
        return double.IsFinite(grade) ? grade : null;
    }

    public static EnvironmentalExposure? SummarizeHeatExposure(
        IReadOnlyList<double> temperatureCelsius, IReadOnlyList<double> supportedMinutes, double thresholdCelsius)
    {
        ArgumentNullException.ThrowIfNull(temperatureCelsius);
        ArgumentNullException.ThrowIfNull(supportedMinutes);
        if (!double.IsFinite(thresholdCelsius) || temperatureCelsius.Count == 0 ||
            temperatureCelsius.Count != supportedMinutes.Count ||
            temperatureCelsius.Any(value => !double.IsFinite(value) || value < -40 || value > 60) ||
            supportedMinutes.Any(value => !double.IsFinite(value) || value <= 0))
        {
            return null;
        }

        double total = 0;
        double above = 0;
        double dose = 0;
        for (var index = 0; index < temperatureCelsius.Count; index++)
        {
            var duration = supportedMinutes[index];
            total += duration;
            if (temperatureCelsius[index] > thresholdCelsius)
            {
                above += duration;
                dose += (temperatureCelsius[index] - thresholdCelsius) * duration;
            }
        }

        return double.IsFinite(total) && double.IsFinite(dose)
            ? new EnvironmentalExposure(total, above, dose)
            : null;
    }
}
