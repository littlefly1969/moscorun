namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScienceDiagnosticSnapshot(Guid DeviceId, DateTimeOffset ReceivedAt, ScienceDiagnosticReport Report);
