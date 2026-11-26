namespace MoscoRun.Domain.Analysis;

public sealed record AttestedPerformanceProfile(string SpeedMethodVersion, string RobustSpeedMethodVersion,
    string CooperMethodVersion, CriticalSpeedFit? OrdinaryLeastSquares, CriticalSpeedFit? RobustCriticalSpeed,
    IReadOnlyList<CooperTestResult> CooperTests, IReadOnlyList<Guid> CriticalSpeedCandidateIds, string PowerMethodVersion, CriticalPowerFit? CriticalPower,
    IReadOnlyList<Guid> CriticalPowerCandidateIds);
