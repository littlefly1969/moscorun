namespace MoscoRun.Domain.Analysis;

/// <summary>Evento riportato; null significa quantità ignota, non assenza del nutriente.</summary>
public sealed record NutritionEvent(
    DateTimeOffset Time,
    DateTimeOffset AvailableAt,
    double? CarbohydrateGrams,
    double? FluidMilliliters,
    double? SodiumMilligrams);
