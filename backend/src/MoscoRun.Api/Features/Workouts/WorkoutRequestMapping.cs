using MoscoRun.Domain.Common;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutRequestMapping
{
    /// <summary>
    /// Converte la richiesta in un allenamento da validare. Elementi nulli o incompleti all'interno di
    /// liste diventano errori di campo: non devono mai arrivare al dominio né produrre un 500.
    /// </summary>
    public static Result<WorkoutRecording> ToRecording(this IngestWorkoutRequest r)
    {
        var errors = new List<DomainError>();
        var track = ReadTrack(r.Track ?? [], errors);
        var series = ReadSeries(r.Series ?? [], errors);

        TimeSpan? active = null;
        if (r.ActiveDurationSeconds is { } seconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0 || seconds > Workout.MaxDuration.TotalSeconds)
            {
                errors.Add(new DomainError("workout.active_duration.invalid", "activeDurationSeconds fuori intervallo.", "activeDurationSeconds"));
            }
            else
            {
                active = TimeSpan.FromSeconds(seconds);
            }
        }

        if (errors.Count > 0)
        {
            return Result<WorkoutRecording>.Failure(errors);
        }

        return Result<WorkoutRecording>.Success(new WorkoutRecording(
            r.Source!,
            r.SourceId!,
            r.Revision!.Value,
            r.ActivityType!,
            r.StartedAt!.Value,
            r.EndedAt!.Value,
            r.UtcOffsetMinutes,
            active,
            r.DistanceM,
            r.EnergyKcal,
            r.AverageHeartRate,
            r.MaxHeartRate,
            track,
            series));
    }

    private static List<TrackPoint> ReadTrack(IReadOnlyList<TrackPointInput?> input, List<DomainError> errors)
    {
        var track = new List<TrackPoint>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            if (input[i] is not { Time: { } time, Lat: { } lat, Lon: { } lon } p)
            {
                errors.Add(new DomainError("workout.track.point_incomplete", $"Punto {i}: time, lat e lon sono obbligatori.", "track"));
                break;
            }

            track.Add(new TrackPoint(time, lat, lon, p.AltitudeM, p.AccuracyM));
        }

        return track;
    }

    private static Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> ReadSeries(IReadOnlyList<SeriesInput?> input, List<DomainError> errors)
    {
        var series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>>();
        foreach (var s in input)
        {
            if (s is not { Metric: { } metric, Samples: { } samples })
            {
                errors.Add(new DomainError("workout.series.incomplete", "Ogni serie richiede metric e samples.", "series"));
                break;
            }

            if (series.ContainsKey(metric))
            {
                errors.Add(new DomainError("workout.series.duplicate", $"Serie {metric} ripetuta.", "series"));
                break;
            }

            var list = new List<SeriesSample>(samples.Count);
            foreach (var x in samples)
            {
                if (x is not { Time: { } time, Value: { } value })
                {
                    errors.Add(new DomainError("workout.series.sample_incomplete", "Ogni campione richiede time e value.", $"series.{metric}"));
                    break;
                }

                list.Add(new SeriesSample(time, value));
            }

            series[metric] = list;
        }

        return series;
    }
}
