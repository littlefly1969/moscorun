using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Ultima esecuzione per dispositivo e sorgente. Nessuna misura o testo libero nei report.</summary>
public sealed class ScienceDiagnosticStore(MoscoRunDbContext db, TimeProvider clock)
{
    public static bool IsValid(ScienceDiagnosticReport report, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!WearableIngestCatalog.IsSource(report.Source) || report.RunId == Guid.Empty ||
            report.StartedAt == default || report.FinishedAt < report.StartedAt ||
            report.FinishedAt > now.AddMinutes(5) || report.FinishedAt - report.StartedAt > TimeSpan.FromDays(1) ||
            report.State is not ("COMPLETED" or "MORE_WORK" or "SKIPPED" or "RETRY_LATER" or "AUTH_REQUIRED") ||
            report.WindowsRead is < 0 or > 10000 ||
            (report.WindowsRead == 0 && (report.FirstReadFrom.HasValue || report.ReadThrough.HasValue)) ||
            (report.WindowsRead > 0 && (report.FirstReadFrom is null || report.ReadThrough is null ||
                report.FirstReadFrom >= report.ReadThrough || report.ReadThrough > report.FinishedAt)) ||
            report.Metrics is null || report.Metrics.Count is < 1 or > 100)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in report.Metrics)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Metric) || !ScientificMetricCatalog.IsKnown(item.Metric) ||
                !seen.Add(item.Metric) || item.Permission is not ("granted" or "denied" or "unknown") ||
                item.Read < 0 || item.Attempted < 0 || item.Attempted > item.Read ||
                item.Rejected < 0 || item.Rejected > item.Attempted || item.Confirmed < 0 ||
                item.Confirmed > item.Attempted - item.Rejected || item.Conflicts < 0 || item.Conflicts > item.Rejected ||
                ((report.WindowsRead == 0 || !string.Equals(item.Permission, "granted", StringComparison.Ordinal)) && item.Read != 0))
            {
                return false;
            }
        }

        return true;
    }

    public Task<bool> SaveAsync(Guid ownerId, Guid deviceId, ScienceDiagnosticReport report, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (!IsValid(report, now) || ownerId == Guid.Empty || deviceId == Guid.Empty)
        {
            throw new ArgumentException("Diagnostica non valida.", nameof(report));
        }

        return ScientificWriteGate.RunAsync(db, ownerId, async token =>
        {
            if (!await db.Devices.AnyAsync(d => d.Id == deviceId && d.OwnerId == ownerId &&
                    d.Kind == DeviceKind.PhoneBridge && d.RevokedAt == null, token))
            {
                throw new ArgumentException("Dispositivo non autorizzato.", nameof(deviceId));
            }

            var payload = JsonSerializer.Serialize(report);
            var started = ScientificModelJson.Normalize(report.StartedAt);
            var rows = db.Set<ScienceDiagnosticRow>();
            var existing = await rows.SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.DeviceId == deviceId &&
                x.Source == report.Source, token);
            if (existing is not null)
            {
                if (existing.RunId == report.RunId)
                {
                    return ScientificModelJson.Equal(existing.Payload, payload);
                }

                if (started <= existing.StartedAt)
                {
                    return false;
                }

                existing.RunId = report.RunId;
                existing.StartedAt = started;
                existing.ReceivedAt = ScientificModelJson.Normalize(now);
                existing.Payload = payload;
            }
            else
            {
                rows.Add(new ScienceDiagnosticRow
                {
                    OwnerId = ownerId,
                    DeviceId = deviceId,
                    Source = report.Source,
                    RunId = report.RunId,
                    StartedAt = started,
                    ReceivedAt = ScientificModelJson.Normalize(now),
                    Payload = payload,
                });
            }

            await db.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<ScienceDiagnosticSnapshot>> ReadAsync(Guid ownerId, DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        var rows = await db.Set<ScienceDiagnosticRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.ReceivedAt <= asOf)
            .OrderByDescending(x => x.ReceivedAt).ToListAsync(ct);
        return rows.Select(x => new ScienceDiagnosticSnapshot(x.DeviceId, x.ReceivedAt,
            JsonSerializer.Deserialize<ScienceDiagnosticReport>(x.Payload) ?? throw new InvalidDataException("Diagnostica non valida.")))
            .ToArray();
    }
}
