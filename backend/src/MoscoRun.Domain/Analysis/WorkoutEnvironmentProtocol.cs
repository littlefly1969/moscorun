namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutEnvironmentProtocol(double HeatThresholdCelsius, double MaximumWeatherGapSeconds,
    double MaximumGpsGapSeconds, double MaximumHorizontalAccuracyMeters, double MinimumHorizontalDistanceMeters,
    bool WeatherRepresentative, bool WindDirectionIsFrom)
{
    public bool IsValid() => double.IsFinite(HeatThresholdCelsius) && HeatThresholdCelsius is >= -40 and <= 60 &&
        double.IsFinite(MaximumWeatherGapSeconds) && MaximumWeatherGapSeconds is >= 1 and <= 3600 &&
        double.IsFinite(MaximumGpsGapSeconds) && MaximumGpsGapSeconds is >= 1 and <= 60 &&
        double.IsFinite(MaximumHorizontalAccuracyMeters) && MaximumHorizontalAccuracyMeters is > 0 and <= 100 &&
        double.IsFinite(MinimumHorizontalDistanceMeters) && MinimumHorizontalDistanceMeters is > 0 and <= 500;
}
