namespace MoscoRun.Domain.Analysis;

/// <summary>Stadio completato di un protocollo incrementale, con lattato misurato e non inferito dalla FC.</summary>
public sealed record LactateStage(double SpeedMetersPerSecond, double LactateMillimolesPerLiter, double HeartRateBpm);
