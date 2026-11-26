namespace MoscoRun.Domain.Analysis;

public sealed record FuelingSummary(
    int EventCount,
    double? CarbohydrateGramsPerHour,
    double? FluidMillilitersPerHour,
    double? SodiumMilligramsPerHour);
