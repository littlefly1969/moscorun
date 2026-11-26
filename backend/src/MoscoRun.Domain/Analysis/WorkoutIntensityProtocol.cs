using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutIntensityProtocol(
    SeriesMetric Metric, IReadOnlyList<double> UpperBoundaries, double? RestingBpm, double? MaximumBpm,
    double? TrimpA, double? TrimpB)
{
    public bool IsValid()
    {
        if (Metric is not (SeriesMetric.HeartRate or SeriesMetric.Speed or SeriesMetric.Power) ||
            UpperBoundaries is null || UpperBoundaries.Count is < 1 or > 10 ||
            UpperBoundaries.Any(value => !Positive(value)))
        {
            return false;
        }

        for (var index = 1; index < UpperBoundaries.Count; index++)
        {
            if (UpperBoundaries[index] <= UpperBoundaries[index - 1])
            {
                return false;
            }
        }

        if (RestingBpm is null && MaximumBpm is null && TrimpA is null && TrimpB is null)
        {
            return true;
        }

        return Metric == SeriesMetric.HeartRate && Positive(RestingBpm) && Positive(MaximumBpm) &&
            MaximumBpm > RestingBpm &&
            ((TrimpA is null && TrimpB is null) || (Positive(TrimpA) && Positive(TrimpB)));
    }

    private static bool Positive(double? value) => value is { } number && double.IsFinite(number) && number > 0;
}
