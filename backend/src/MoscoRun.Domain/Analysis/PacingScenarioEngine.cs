namespace MoscoRun.Domain.Analysis;

/// <summary>Scenari di tempo e costi relativi dichiarati, non prescrizioni ottimizzate.</summary>
public static class PacingScenarioEngine
{
    public const string MethodVersion = "conditional-pacing-scenario-v1";

    public static bool Valid(PacingScenarioRequest request, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Segments is null || request.Segments.Count is < 1 or > 200 ||
            request.Segments.Any(item => item is null || !double.IsFinite(item.DistanceMeters) ||
                item.DistanceMeters <= 0 || !double.IsFinite(item.RelativeTimeCost) || item.RelativeTimeCost is < 0.1 or > 10) ||
            request.TargetSeconds.HasValue == (request.RaceReference is not null))
        {
            return false;
        }

        var distance = request.Segments.Sum(item => item.DistanceMeters);
        if (!double.IsFinite(distance) || distance is < 100 or > 100000)
        {
            return false;
        }

        if (request.TargetSeconds is { } seconds)
        {
            return double.IsFinite(seconds) && seconds is > 0 and <= 604800;
        }

        var reference = request.RaceReference!;
        return !string.IsNullOrWhiteSpace(reference.Source) && reference.Source.Length <= 32 &&
            AttestedRaceEngine.ValidWindow(At(reference.From), At(reference.Split), At(reference.Until), asOf, distance);
    }

    public static PacingScenarioReport? Build(PacingScenarioRequest request, DateTimeOffset asOf, AttestedRaceReport? race)
    {
        if (!Valid(request, asOf))
        {
            return null;
        }

        var distance = request.Segments.Sum(item => item.DistanceMeters);
        if (request.RaceReference is { } reference &&
            (race is null || !string.Equals(race.Source, reference.Source, StringComparison.Ordinal) ||
                race.TrainingStart != At(reference.From) || race.Split != At(reference.Split) ||
                race.End != At(reference.Until) || race.TargetMeters != distance || race.AsOf != asOf))
        {
            return null;
        }

        var prediction = request.RaceReference is null ? null : race?.Prediction;
        var target = request.TargetSeconds ?? prediction?.PredictedSeconds;
        var splits = target is { } seconds ? PacingEngine.Allocate(request.Segments, seconds) : null;
        var status = target is null ? "insufficient-calibration" : splits is null ? "invalid-allocation" : "conditional-scenario";
        return new PacingScenarioReport(MethodVersion, asOf, status,
            request.RaceReference is null ? "declared-target" : "personal-riegel", distance, target,
            request.Segments, splits ?? [], prediction, request.RaceReference is null ? null : race?.Interval,
            request.RaceReference is null ? [] : race?.Evidence ?? []);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
