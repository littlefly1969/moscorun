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
        var archive = db.Set<ScientificObservationRow>().AsNoTracking();
        var query = archive.Where(x => x.OwnerId == ownerId &&
            x.Metric == metric && x.Source == source && x.AvailableAt <= asOf);
        if (beforeTime is { } time && beforeId is { } id)
        {
            query = query.Where(x => x.Time < time || (x.Time == time && x.Id.CompareTo(id) < 0));
        }

        // Only an exact coincidence of point measures with shared Samsung app provenance is a candidate.
        // Never merge/delete records or treat overlapping intervals/aggregates as identical.
        var pointMetrics = new[] { "heartRate", "restingHeartRate", "spo2", "weight", "fatPercentage", "fatFreeMass",
            "bodyWater", "bodyTemperature", "skinTemperature", "bloodGlucose", "systolicBloodPressure", "diastolicBloodPressure" };
        var checkDuplicates = pointMetrics.Contains(metric, StringComparer.Ordinal) && source is "health-connect" or "samsung-health";
        var counterpart = string.Equals(source, "health-connect", StringComparison.Ordinal) ? "samsung-health" : "health-connect";
        var rows = await query.OrderByDescending(x => x.Time).ThenByDescending(x => x.Id).Take(limit + 1)
            .Select(x => new InventoryObservation(x.Id, x.Metric, x.Unit, x.Value, x.Source, x.Method,
                x.ProviderAlgorithm, x.ImportDeviceId, x.WorkoutId, x.Time, x.AvailableAt, x.ExternalRevision, x.ExternalRevisionOrder,
                x.NativeMetric, x.NativeUnit, x.NativeValue, x.MappingVersion,
                checkDuplicates && x.Value != null && x.WorkoutId == null && x.ProviderAlgorithm == "com.sec.android.app.shealth"
                    ? archive.Any(other => other.OwnerId == ownerId && other.Source == counterpart && other.Metric == metric &&
                        other.Time == x.Time && other.Unit == x.Unit && other.Value == x.Value && other.WorkoutId == null &&
                        other.ProviderAlgorithm == x.ProviderAlgorithm && other.AvailableAt <= asOf)
                    : null))
            .ToListAsync(ct);
        return new InventoryObservationPage(asOf, rows.Take(limit).ToArray(), rows.Count > limit);
    }
}
