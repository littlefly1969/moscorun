using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class ExtendedBiomechanicsReader(MoscoRunDbContext db)
{
    private const int MaxChunks = 64;

    public async Task<ExtendedBiomechanicsReport?> ReadAsync(Guid owner, Guid workoutId, string source, DateTimeOffset asOf,
        IReadOnlyList<Guid> ids, bool protocolConfirmed, bool cadenceIsStepsPerMinute, bool balanceIsLeftPercent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (owner == Guid.Empty || workoutId == Guid.Empty || string.IsNullOrWhiteSpace(source) || source.Length > 200 ||
            ids.Count > 5 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var report = await ReadCoreAsync(owner, workoutId, source, asOf, ids, protocolConfirmed,
                cadenceIsStepsPerMinute, balanceIsLeftPercent, token);
            await tx.CommitAsync(token);
            return report;
        }, ct);
    }

    private async Task<ExtendedBiomechanicsReport?> ReadCoreAsync(Guid owner, Guid workoutId, string source,
        DateTimeOffset asOf, IReadOnlyList<Guid> ids, bool confirmed, bool cadence, bool balance, CancellationToken ct)
    {
        var workout = await db.Workouts.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerId == owner &&
            row.Id == workoutId && row.EndedAt <= asOf && row.UpdatedAt <= asOf, ct);
        if (workout is null)
        {
            return null;
        }

        // I chunk sono a 100 ns, l'allenamento al millisecondo: la finestra si confronta alla risoluzione dell'allenamento.
        var first = WorkoutTime.Floor(workout.StartedAt).UtcTicks;
        var endExclusive = WorkoutTime.ExclusiveEndTicks(workout.EndedAt);
        var query = db.Set<ScientificSignalChunkRow>().AsNoTracking().Where(row =>
            row.OwnerId == owner && row.WorkoutId == workoutId && row.Source == source &&
            row.FirstTicks >= first && row.LastTicks < endExclusive &&
            row.AvailableTicks <= asOf.UtcTicks &&
            (row.Metric == "speed" || row.Metric == "cadence" || row.Metric == "groundContactTime" ||
             row.Metric == "verticalOscillation" || row.Metric == "groundContactBalance"));
        var chunks = await query.OrderBy(row => row.FirstTicks).ThenBy(row => row.Id).Take(MaxChunks + 1)
            .Select(row => new BiomechanicsChunkMetadata(row.Id, row.Metric, row.Unit, row.ExternalId, row.Revision,
                row.RevisionOrder, row.Method, row.ProviderAlgorithm, row.DeviceId,
                new DateTimeOffset(row.FirstTicks, TimeSpan.Zero), new DateTimeOffset(row.LastTicks, TimeSpan.Zero),
                new DateTimeOffset(row.AvailableTicks, TimeSpan.Zero), row.SampleCount, Convert.ToHexStringLower(row.ContentHash)))
            .ToListAsync(ct);
        var selected = chunks.Take(MaxChunks).Where(item => ids.Contains(item.Id)).ToArray();
        if (selected.Length != ids.Count)
        {
            return null;
        }

        var status = Status(workout, selected, confirmed);
        IReadOnlyList<BiomechanicsMetricSummary> metrics = [];
        if (string.Equals(status, "ready", StringComparison.Ordinal))
        {
            var rows = await query.Where(row => ids.Contains(row.Id)).ToListAsync(ct);
            var channels = rows.Select(Channel).ToArray();
            metrics = ExtendedBiomechanicsEngine.Build(workout.StartedAt, workout.EndedAt, channels, cadence, balance) ?? [];
            status = metrics.Any(item => item.WeightedMean.HasValue) ? "observed" : "insufficient-support";
        }

        return new ExtendedBiomechanicsReport(workoutId, workout.Revision, Convert.ToHexStringLower(workout.ContentHash),
            source, asOf, ExtendedBiomechanicsEngine.MethodVersion, status, (workout.EndedAt - workout.StartedAt).TotalSeconds,
            chunks.Count > MaxChunks, confirmed, cadence, balance, Array.AsReadOnly(chunks.Take(MaxChunks).ToArray()),
            Array.AsReadOnly(selected), metrics);
    }

    private static string Status(Workout workout, BiomechanicsChunkMetadata[] selected, bool confirmed)
    {
        if (!string.Equals(workout.ActivityType, "RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            return "unsupported-activity";
        }

        if (selected.Length == 0)
        {
            return "select-chunks";
        }

        if (!confirmed)
        {
            return "unconfirmed-protocol";
        }

        return selected.Select(item => item.Metric).Distinct(StringComparer.Ordinal).Count() != selected.Length ||
            selected.Any(item => !item.DeviceId.HasValue ||
                !ScientificMetricCatalog.AcceptsUnit(item.Metric, item.Unit) ||
                item.Method is not (nameof(ObservationMethod.Measured) or nameof(ObservationMethod.ProviderReported))) ||
            selected.Select(item => item.DeviceId).Distinct().Count() != 1
            ? "incompatible-chunks" : "ready";
    }

    private static BiomechanicsChannel Channel(ScientificSignalChunkRow row) =>
        new(row.Metric, ScientificSignalCodec.Decode(row.Data, row.ContentHash, row.SampleCount, row.CodecVersion)
            .Select(item => new SignalSample(WorkoutTime.Floor(item.Time), item.Value)).ToArray());
}
