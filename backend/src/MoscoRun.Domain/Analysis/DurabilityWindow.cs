using System.Runtime.InteropServices;

namespace MoscoRun.Domain.Analysis;

/// <summary>Finestra con medie calcolate sul medesimo supporto temporale dei due canali.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DurabilityWindow(
    DateTimeOffset Start,
    DateTimeOffset End,
    TimeSpan PairedDuration,
    double MeanExternalLoad,
    double MeanHeartRateBpm,
    Guid SessionId,
    ExternalLoadMetric ExternalLoadMetric);
