namespace MoscoRun.Domain.Analysis;

public static class NutritionFeedbackMetrics
{
    public const string Complete = "nutritionDiaryComplete";
    public const string Thermogenesis = "thermicEffectOfFood";
    public const string NonExerciseNet = "nonExerciseEnergyExpenditureNet";
    public const string ExerciseNet = "exerciseEnergyExpenditureNet";
    public const string BeforeWeight = "preExerciseWeight";
    public const string AfterWeight = "postExerciseWeight";
    public const string Urine = "exerciseUrineVolume";
    public const string SweatProtocol = "sweatProtocolConfirmed";

    public static bool IsAttestation(string metric) =>
        string.Equals(metric, Complete, StringComparison.Ordinal) ||
        string.Equals(metric, SweatProtocol, StringComparison.Ordinal) ||
        string.Equals(metric, AttestedRaceEngine.AttestationMetric, StringComparison.Ordinal) ||
        string.Equals(metric, ScientificFeedbackKeys.DailyCompleteMetric, StringComparison.Ordinal);
}
