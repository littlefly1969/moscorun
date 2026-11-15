namespace MoscoRun.Domain.Workouts;

/// <summary>Tracciato GPS completo di un allenamento, nel formato di <see cref="TrackCodec"/>.</summary>
public sealed class WorkoutRoute
{
    private WorkoutRoute()
    {
        Data = [];
    }

    internal WorkoutRoute(Guid workoutId, IReadOnlyList<TrackPoint> normalizedPoints)
    {
        WorkoutId = workoutId;
        PointCount = normalizedPoints.Count;
        Data = TrackCodec.Encode(normalizedPoints);
    }

    public Guid WorkoutId { get; private set; }

    public int PointCount { get; private set; }

    public byte[] Data { get; private set; }

    public IReadOnlyList<TrackPoint> Points() => TrackCodec.Decode(Data);
}
