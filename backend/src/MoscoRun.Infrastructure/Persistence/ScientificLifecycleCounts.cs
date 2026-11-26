namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificLifecycleCounts(int Observations = 0, int FeedbackEntries = 0, int Signals = 0,
    int ModelRuns = 0, int ModelArtifacts = 0, int Checkpoints = 0, int ProtectedObservations = 0, int FenceChanges = 0);
