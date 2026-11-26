namespace MoscoRun.Domain.Analysis;

public sealed record PersonalResearchReport(string MethodVersion, string ModelVersion, string Source, DateTimeOffset AsOf,
    DateOnly FirstDay, DateOnly SplitDay, DateOnly UntilDay, double DistanceMeters, double Penalty, string Status,
    IReadOnlyList<string> Features, int CandidateTests, IReadOnlyList<ResearchExampleEvidence> Examples,
    IReadOnlyList<PersonalResearchFold> Folds);
