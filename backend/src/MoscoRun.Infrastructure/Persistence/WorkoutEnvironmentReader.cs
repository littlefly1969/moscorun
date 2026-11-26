using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class WorkoutEnvironmentReader(MoscoRunDbContext db)
{
    private const int MaxChunks = 64;

    public async Task<WorkoutEnvironmentReport?> ReadAsync(Guid owner, Guid workoutId, WorkoutEnvironmentRequest request,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (owner == Guid.Empty || workoutId == Guid.Empty || !request.IsValid())
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var result = await ReadCoreAsync(owner, workoutId, request, asOf, token);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }

    private async Task<WorkoutEnvironmentReport?> ReadCoreAsync(Guid owner, Guid workoutId,
        WorkoutEnvironmentRequest request, DateTimeOffset asOf, CancellationToken ct)
    {
        var workout = await db.Workouts.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerId == owner &&
            row.Id == workoutId && row.EndedAt <= asOf && row.UpdatedAt <= asOf, ct);
        if (workout is null || !string.Equals(workout.ActivityType, "RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var route = await db.WorkoutRoutes.AsNoTracking().SingleOrDefaultAsync(row => row.WorkoutId == workoutId, ct);
        var query = db.Set<ScientificSignalChunkRow>().AsNoTracking().Where(row =>
            row.OwnerId == owner && row.WorkoutId == workoutId && row.Source == request.Source &&
            row.FirstTicks < workout.EndedAt.UtcTicks && row.LastTicks > workout.StartedAt.UtcTicks &&
            row.LastTicks <= asOf.UtcTicks && row.AvailableTicks <= asOf.UtcTicks &&
            (row.Metric == "airTemperature" || row.Metric == "humidity" || row.Metric == "windSpeed" || row.Metric == "windDirection"));
        var chunks = await query.OrderBy(row => row.FirstTicks).ThenBy(row => row.Id).Take(MaxChunks + 1)
            .Select(row => new BiomechanicsChunkMetadata(row.Id, row.Metric, row.Unit, row.ExternalId, row.Revision,
                row.RevisionOrder, row.Method, row.ProviderAlgorithm, row.DeviceId,
                new DateTimeOffset(row.FirstTicks, TimeSpan.Zero), new DateTimeOffset(row.LastTicks, TimeSpan.Zero),
                new DateTimeOffset(row.AvailableTicks, TimeSpan.Zero), row.SampleCount, Convert.ToHexStringLower(row.ContentHash)))
            .ToListAsync(ct);
        var selected = chunks.Take(MaxChunks).Where(item => request.ChunkIds.Contains(item.Id)).ToArray();
        if (selected.Length != request.ChunkIds.Count)
        {
            return null;
        }

        var status = Status(request, selected);
        var rows = string.Equals(status, "ready", StringComparison.Ordinal)
            ? await query.Where(row => request.ChunkIds.Contains(row.Id)).ToListAsync(ct) : [];
        var channels = rows.Select(Channel).ToArray();
        var analysis = WorkoutEnvironmentEngine.Build(workout.StartedAt, workout.EndedAt,
            route?.Points() ?? [], channels, request.Protocol);
        if (analysis is null)
        {
            return null;
        }

        if (string.Equals(status, "ready", StringComparison.Ordinal))
        {
            status = analysis.Metrics.Any(metric => metric.WeightedMean.HasValue) ? "observed-support" : "insufficient-support";
        }

        return new WorkoutEnvironmentReport(workout.Id, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            asOf, request.Source, status, request.Protocol, chunks.Count > MaxChunks,
            Array.AsReadOnly(chunks.Take(MaxChunks).ToArray()), Array.AsReadOnly(selected), analysis);
    }

    private static string Status(WorkoutEnvironmentRequest request, BiomechanicsChunkMetadata[] selected)
    {
        if (selected.Length == 0)
        {
            return "select-chunks";
        }

        if (!request.Protocol.WeatherRepresentative)
        {
            return "unconfirmed-representativeness";
        }

        return selected.Select(item => item.Metric).Distinct(StringComparer.Ordinal).Count() != selected.Length ||
            selected.Any(item => !ScientificMetricCatalog.AcceptsUnit(item.Metric, item.Unit) ||
                item.Method is not (nameof(ObservationMethod.Measured) or nameof(ObservationMethod.ProviderReported) or
                    nameof(ObservationMethod.ProviderEstimated))) ? "incompatible-chunks" : "ready";
    }

    private static WeatherChannel Channel(ScientificSignalChunkRow row) =>
        new(row.Metric, ScientificSignalCodec.Decode(row.Data, row.ContentHash, row.SampleCount, row.CodecVersion)
            .Select(item => new SignalSample(item.Time, item.Value)).ToArray());
}
