namespace MoscoRun.Domain.Analysis;

/// <summary>Test e retest già abbinati secondo lo stesso protocollo; perdita in punti percentuali.</summary>
public sealed record DurabilityRepeat(
    Guid OwnerId, Guid TestSessionId, Guid RetestSessionId, string ProtocolId, string Source,
    ExternalLoadMetric Metric, double TestLossPercent, double RetestLossPercent);
