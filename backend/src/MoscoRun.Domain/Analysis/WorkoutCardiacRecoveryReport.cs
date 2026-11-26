namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutCardiacRecoveryReport(Guid WorkoutId, long Revision, string ContentSha256, string Source,
    DateTimeOffset AsOf, string MethodVersion, double MaximumAgeSeconds, WorkoutCardiacRecoveryProtocol Protocol,
    string Status, IReadOnlyList<CardiacRecoveryPoint> Points, CardiacResponseFit? Fit, string FitStatus);
