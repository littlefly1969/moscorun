namespace MoscoRun.Domain.Analysis;

public sealed record NutritionFeedbackReport(
    string MethodVersion, string Source, string Status, bool DiaryComplete, double? DurationHours,
    double? TotalExpenditureKcal, double? EnergyBalanceKcal, double? EnergyAvailabilityKcalPerKg,
    double? CarbohydrateGramsPerHour, double? FluidMillilitersPerHour, double? SodiumMilligramsPerHour,
    double? EstimatedSweatLitersPerHour);
