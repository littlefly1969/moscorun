namespace MoscoRun.Domain.Analysis;

/// <summary>
/// Che cosa un connettore wearable può scrivere direttamente: un sottoinsieme del catalogo athlete-analysis v1.
/// Le scale soggettive e le attestazioni restano riservate al diario dell'atleta, le grandezze derivate ai motori.
/// </summary>
public static class WearableIngestCatalog
{
    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal)
    {
        "garmin-connect", "samsung-health", "health-connect",
    };

    private static readonly HashSet<string> ObservationMetrics = new(StringComparer.Ordinal)
    {
        "heartRate", "restingHeartRate", "hrvRmssd", "hrvSdnn", "spo2", "respirationRate", "skinTemperature",
        "steps", "distance", "altitude",
        "sleepDuration", "sleepEfficiency", "sleepStageDuration", "sleepInBedDuration", "napDuration",
        "sedentaryDuration", "activeDuration",
        "vo2max", "lactateThresholdHeartRate", "lactateThresholdSpeed", "aerobicTrainingEffect",
        "anaerobicTrainingEffect", "trainingLoad", "recoveryTime", "bodyBattery", "stress", "sleepScore",
        "enduranceScore", "hillScore",
        "energyExpenditure", "restingEnergyExpenditure", "sweatLoss",
        "weight", "impedance", "fatMass", "fatPercentage", "fatFreeMass", "muscleMass", "bodyWater",
        "bodyWaterPercentage", "boneMass", "visceralFat",
        "energyIntake", "carbohydrateIntake", "proteinIntake", "fatIntake", "fluidIntake", "sodiumIntake",
        "caffeineIntake",
        "airTemperature", "humidity", "windSpeed", "windDirection", "barometricPressure",
        "basalMetabolicRate",
        "height",
        "bodyMassIndex",
        "fatFreePercentage",
        "musclePercentage",
        "skeletalMusclePercentage",
        "skeletalMuscleMass",
        "bodyWaterVolume",
        "systolicBloodPressure",
        "diastolicBloodPressure",
        "meanBloodPressure",
        "pulseRate",
        "bodyTemperature",
        "energyScore",
        "bloodGlucose",
        "floorsClimbed",
        "activeEnergyExpenditure",
        "fiberIntake",
        "sugarIntake",
        "saturatedFatIntake",
        "irregularRhythmNotification",
        "sleepApneaRiskSign",
    };

    private static readonly HashSet<string> SignalMetrics = new(StringComparer.Ordinal)
    {
        "heartRate", "rrInterval", "spo2", "respirationRate", "skinTemperature",
        "speed", "distance", "cadence", "power", "altitude", "grade",
        "groundContactTime", "groundContactBalance", "verticalOscillation", "verticalRatio", "stepLength",
        "strideLength", "stepSpeedLoss", "stepSpeedLossPercent",
        "accelerationX", "accelerationY", "accelerationZ", "angularVelocityX", "angularVelocityY", "angularVelocityZ",
        "ppg", "ecg", "eda", "muscleOxygenSaturation",
        "airTemperature", "humidity", "windSpeed", "windDirection", "barometricPressure",
    };

    public static IReadOnlyCollection<string> SupportedSources => Sources;

    public static bool IsSource(string? source) => source is not null && Sources.Contains(source);

    /// <summary>Metrica ammessa come osservazione puntuale, con l'unità canonica del catalogo.</summary>
    public static bool AcceptsObservation(string? metric, string? unit) =>
        metric is not null && unit is not null && ObservationMetrics.Contains(metric) &&
        ScientificMetricCatalog.IsKnown(metric) && ScientificMetricCatalog.AcceptsUnit(metric, unit);

    /// <summary>Metrica ammessa come segnale ad alta frequenza, con l'unità canonica del catalogo.</summary>
    public static bool AcceptsSignal(string? metric, string? unit) =>
        metric is not null && unit is not null && SignalMetrics.Contains(metric) &&
        ScientificMetricCatalog.IsKnown(metric) && ScientificMetricCatalog.AcceptsUnit(metric, unit);

    /// <summary>Solo misure e dati del produttore: le stime derivate o inferite nascono sul server.</summary>
    public static bool AcceptsMethod(ObservationMethod method) =>
        method is ObservationMethod.Measured or ObservationMethod.ProviderReported or ObservationMethod.ProviderEstimated;
}
