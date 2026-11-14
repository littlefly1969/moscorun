namespace MoscoRun.Domain.Workouts;

/// <summary>Allenamento validato con tracciato e serie pronti da salvare.</summary>
public sealed record WorkoutContent(
    Workout Workout,
    WorkoutRoute? Route,
    IReadOnlyList<WorkoutSeries> Series,
    WorkoutRecording Recording);
