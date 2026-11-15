namespace MoscoRun.Domain.Workouts;

/// <summary>Serie temporale di una grandezza, nel formato di <see cref="SeriesCodec"/>.</summary>
public sealed class WorkoutSeries
{
    private WorkoutSeries()
    {
        Data = [];
    }

    internal WorkoutSeries(Guid workoutId, SeriesMetric metric, IReadOnlyList<SeriesSample> normalizedSamples)
    {
        WorkoutId = workoutId;
        Metric = metric;
        SampleCount = normalizedSamples.Count;
        Data = SeriesCodec.Encode(normalizedSamples);
    }

    public Guid WorkoutId { get; private set; }

    public SeriesMetric Metric { get; private set; }

    public int SampleCount { get; private set; }

    public byte[] Data { get; private set; }

    public IReadOnlyList<SeriesSample> Samples() => SeriesCodec.Decode(Data);
}
