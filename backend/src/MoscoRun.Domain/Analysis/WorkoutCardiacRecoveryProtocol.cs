using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutCardiacRecoveryProtocol(int EndOffsetSeconds, string Mode, bool Confirmed, double? EquilibriumBpm)
{
    public bool IsValid() =>
        EndOffsetSeconds > 0 && EndOffsetSeconds <= Workout.MaxDuration.TotalSeconds &&
        Mode is "passive-seated" or "passive-standing" or "passive-supine" or "active-walking" &&
        (EquilibriumBpm is null || (double.IsFinite(EquilibriumBpm.Value) && EquilibriumBpm > 0));
}
