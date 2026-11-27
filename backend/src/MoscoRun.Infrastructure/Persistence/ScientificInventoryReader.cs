using Microsoft.EntityFrameworkCore;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Inventario descrittivo dell'archivio, non una selezione scientifica delle revisioni utilizzabili.</summary>
public sealed class ScientificInventoryReader(MoscoRunDbContext db)
{
    public async Task<ScientificInventory> ReadAsync(Guid ownerId, DateTimeOffset asOf, CancellationToken ct = default)
    {
        var observations = await db.Set<ScientificObservationRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.AvailableAt <= asOf)
            .GroupBy(x => new { x.Metric, x.Unit, x.Source, x.Method, x.ImportDeviceId, Session = x.WorkoutId != null })
            .Select(g => new ObservationInventoryGroup(g.Key.Metric, g.Key.Unit, g.Key.Source, g.Key.Method,
                g.Key.ImportDeviceId, g.Key.Session, g.LongCount(), g.LongCount(x => x.Value != null),
                g.Min(x => x.Time), g.Max(x => x.Time), g.Max(x => x.AvailableAt)))
            .ToListAsync(ct);

        var ticks = asOf.UtcTicks;
        var signals = await db.Set<ScientificSignalChunkRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.AvailableTicks <= ticks)
            .GroupBy(x => new { x.Metric, x.Unit, x.Source, x.Method, x.DeviceId, Session = x.WorkoutId != null })
            .Select(g => new
            {
                g.Key.Metric,
                g.Key.Unit,
                g.Key.Source,
                g.Key.Method,
                g.Key.DeviceId,
                g.Key.Session,
                Chunks = g.LongCount(),
                Samples = g.Sum(x => (long)x.SampleCount),
                First = g.Min(x => x.FirstTicks),
                Last = g.Max(x => x.LastTicks),
                Available = g.Max(x => x.AvailableTicks),
            })
            .ToListAsync(ct);

        var scales = await db.Weighings.AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.ReceivedAt <= asOf)
            .GroupBy(x => new { x.DeviceId, x.Attribution })
            .Select(g => new
            {
                g.Key.DeviceId,
                g.Key.Attribution,
                Count = g.LongCount(),
                WithImpedance = g.LongCount(x => x.ImpedanceOhm != null),
                First = g.Min(x => x.MeasuredAt),
                Last = g.Max(x => x.MeasuredAt),
                Received = g.Max(x => x.ReceivedAt),
            })
            .ToListAsync(ct);

        var devices = await db.Devices.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.Name).Select(x => new InventoryDevice(x.Id, x.Name, x.LastSeenAt, x.RevokedAt)).ToListAsync(ct);

        var diagnostics = await new ScienceDiagnosticStore(db, TimeProvider.System).ReadAsync(ownerId, asOf, ct);
        return new ScientificInventory(asOf, observations,
            signals.Select(x => new SignalInventoryGroup(x.Metric, x.Unit, x.Source, x.Method, x.DeviceId, x.Session,
                x.Chunks, x.Samples, new DateTimeOffset(x.First, TimeSpan.Zero), new DateTimeOffset(x.Last, TimeSpan.Zero),
                new DateTimeOffset(x.Available, TimeSpan.Zero))).ToArray(),
            scales.Select(x => new ScaleInventoryGroup(x.DeviceId, x.Attribution.ToString(), x.Count, x.WithImpedance,
                x.First, x.Last, x.Received)).ToArray(), devices, diagnostics);
    }

    public async Task<InventoryObservationPage> ReadObservationsAsync(Guid ownerId, string metric, string source,
        DateTimeOffset asOf, DateTimeOffset? beforeTime, Guid? beforeId, int limit, CancellationToken ct = default)
    {
        var query = db.Set<ScientificObservationRow>().AsNoTracking().Where(x => x.OwnerId == ownerId &&
            x.Metric == metric && x.Source == source && x.AvailableAt <= asOf);
        if (beforeTime is { } time && beforeId is { } id)
        {
            query = query.Where(x => x.Time < time || (x.Time == time && x.Id.CompareTo(id) < 0));
        }

        var rows = await query.OrderByDescending(x => x.Time).ThenByDescending(x => x.Id).Take(limit + 1)
            .Select(x => new InventoryObservation(x.Id, x.Metric, x.Unit, x.Value, x.Source, x.Method,
                x.ProviderAlgorithm, x.ImportDeviceId, x.WorkoutId, x.Time, x.AvailableAt, x.ExternalRevision, x.ExternalRevisionOrder))
            .ToListAsync(ct);
        return new InventoryObservationPage(asOf, rows.Take(limit).ToArray(), rows.Count > limit);
    }
}
