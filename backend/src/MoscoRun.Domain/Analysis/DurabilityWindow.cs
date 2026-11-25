namespace MoscoRun.Domain.Analysis;

/// <summary>Finestra con medie calcolate sul medesimo supporto temporale dei due canali.</summary>
public readonly record struct DurabilityWindow(
    DateTimeOffset Start,
    DateTimeOffset End,
    TimeSpan PairedDuration,
    double MeanExternalLoad,
    double MeanHeartRateBpm);
