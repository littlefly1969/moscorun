namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificModelOutput(Guid Id, string Metric, string Unit, double? Value,
    double? Lower = null, double? Upper = null);
