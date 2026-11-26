namespace MoscoRun.Infrastructure.Persistence;

public sealed record AthleteFeedbackSnapshot(AthleteFeedbackEntry Entry, DateTimeOffset AvailableAt);
