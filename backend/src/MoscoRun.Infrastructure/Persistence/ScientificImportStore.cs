using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using Npgsql;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Importazione atomica e ripetibile di revisioni esterne, senza sovrascrivere lo storico.</summary>
public sealed class ScientificImportStore(MoscoRunDbContext db, TimeProvider clock)
{
    public const int MaxBatchSize = 500;

    public static Guid Identity(ScientificImportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.ExternalId) || string.IsNullOrWhiteSpace(item.Revision) ||
            string.IsNullOrWhiteSpace(item.Source) || string.IsNullOrWhiteSpace(item.Metric) ||
            item.ExternalId.Length > 512 || item.Revision.Length > 200 || item.RevisionOrder < 0 ||
            Encoding.UTF8.GetByteCount(item.Source) + Encoding.UTF8.GetByteCount(item.ExternalId) + Encoding.UTF8.GetByteCount(item.Metric) > 2300)
        {
            throw new ArgumentException("Identità esterna incompleta.", nameof(item));
        }

        return new Guid(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new[] { "scientific-import-v1", item.Source, item.ExternalId, item.Revision, item.Metric })).AsSpan(0, 16));
    }

    public async Task<int> ImportAsync(Guid ownerId, IReadOnlyList<ScientificImportItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (ownerId == Guid.Empty || items.Count > MaxBatchSize)
        {
            throw new ArgumentException("Atleta o dimensione del batch non validi.", nameof(items));
        }

        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Il contesto contiene modifiche estranee all'importazione.");
        }

        var observations = Prepare(ownerId, items, Normalize(clock.GetUtcNow()));
        await ValidateProvenanceAsync(ownerId, items, ct);
        for (var attempt = 0; ; attempt++)
        {
            var ids = observations.Select(o => o.Id).ToArray();
            var existing = await db.Set<ScientificObservationRow>().AsNoTracking()
                .Where(o => o.OwnerId == ownerId && ids.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, ct);
            var pending = Pending(ownerId, observations, items, existing);

            if (pending.Count == 0)
            {
                return 0;
            }

            db.Set<ScientificObservationRow>().AddRange(pending);
            try
            {
                await db.SaveChangesAsync(ct);
                return pending.Count;
            }
            catch (DbUpdateException ex) when (attempt < 2 && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                foreach (var row in pending)
                {
                    db.Entry(row).State = EntityState.Detached;
                }
            }
        }
    }

    private static List<ScientificObservationRow> Pending(Guid ownerId, ScientificObservation[] observations,
        IReadOnlyList<ScientificImportItem> items, Dictionary<Guid, ScientificObservationRow> existing)
    {
        var pending = new List<ScientificObservationRow>();
        for (var i = 0; i < observations.Length; i++)
        {
            var observation = observations[i];
            var item = items[i];
            if (existing.TryGetValue(observation.Id, out var saved))
            {
                if (!Matches(saved, observation, item))
                {
                    throw new InvalidOperationException("Revisione già presente con contenuto diverso.");
                }

                continue;
            }

            pending.Add(new ScientificObservationRow
            {
                OwnerId = ownerId,
                Id = observation.Id,
                Metric = observation.Metric,
                Unit = observation.Unit,
                Value = observation.Value,
                Time = observation.Time,
                AvailableAt = observation.AvailableAt,
                Source = observation.Source,
                Method = observation.Method.ToString(),
                ProviderAlgorithm = observation.ProviderAlgorithm,
                ExternalId = item.ExternalId,
                ExternalRevision = item.Revision,
                ExternalRevisionOrder = item.RevisionOrder,
                ImportDeviceId = item.Provenance?.DeviceId,
                WorkoutId = item.Provenance?.WorkoutId,
                NativeMetric = item.Provenance?.NativeMetric,
                NativeUnit = item.Provenance?.NativeUnit,
                NativeValue = item.Provenance?.NativeValue,
                MappingVersion = item.Provenance?.MappingVersion,
            });
        }

        return pending;
    }

    private static ScientificObservation[] Prepare(Guid ownerId, IReadOnlyList<ScientificImportItem> items, DateTimeOffset now)
    {
        var observations = items.Select(item =>
        {
            if (item.Method is not (ObservationMethod.Measured or ObservationMethod.ProviderReported or ObservationMethod.ProviderEstimated or ObservationMethod.SelfReported))
            {
                throw new ArgumentException("Metodo non ammesso per importazione diretta.", nameof(items));
            }

            return new ScientificObservation(Identity(item), ownerId, item.Metric, item.Unit, item.Value,
                Normalize(item.Time), now, item.Source, item.Method, item.ProviderAlgorithm);
        }).ToArray();

        if (observations.Select(o => o.Id).Distinct().Count() != observations.Length)
        {
            throw new ArgumentException("Identificativi duplicati nel batch.", nameof(items));
        }

        return observations;
    }

    private async Task ValidateProvenanceAsync(Guid ownerId, IReadOnlyList<ScientificImportItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var p = item.Provenance;
            if (p is null)
            {
                continue;
            }

            if (p.DeviceId == Guid.Empty || p.WorkoutId == Guid.Empty ||
                p.NativeMetric?.Length > 200 || p.NativeUnit?.Length > 200 || p.MappingVersion?.Length > 200 ||
                (p.NativeValue is { } value && !double.IsFinite(value)) ||
                ((p.NativeMetric is not null || p.NativeUnit is not null || p.NativeValue is not null || p.MappingVersion is not null) &&
                    (string.IsNullOrWhiteSpace(p.NativeMetric) || string.IsNullOrWhiteSpace(p.NativeUnit) ||
                        string.IsNullOrWhiteSpace(p.MappingVersion))))
            {
                throw new ArgumentException("Provenienza non valida.", nameof(items));
            }
        }

        var devices = items.Select(x => x.Provenance?.DeviceId).OfType<Guid>().Distinct().ToArray();
        var workouts = items.Select(x => x.Provenance?.WorkoutId).OfType<Guid>().Distinct().ToArray();
        if (devices.Length > 0 && await db.Devices.CountAsync(d => d.OwnerId == ownerId &&
                d.RevokedAt == null && devices.Contains(d.Id), ct) != devices.Length)
        {
            throw new ArgumentException("Dispositivo non autorizzato.", nameof(items));
        }

        if (workouts.Length > 0 && await db.Workouts.CountAsync(w => w.OwnerId == ownerId &&
                workouts.Contains(w.Id), ct) != workouts.Length)
        {
            throw new ArgumentException("Allenamento non autorizzato.", nameof(items));
        }
    }

    private static DateTimeOffset Normalize(DateTimeOffset time)
    {
        var ticks = time.UtcTicks;
        return new DateTimeOffset(ticks - (ticks % 10), TimeSpan.Zero);
    }

    private static bool Matches(ScientificObservationRow saved, ScientificObservation observation, ScientificImportItem item) =>
        string.Equals(saved.Metric, observation.Metric, StringComparison.Ordinal) && string.Equals(saved.Unit, observation.Unit, StringComparison.Ordinal) && saved.Value == observation.Value &&
        saved.Time == observation.Time && string.Equals(saved.Source, observation.Source, StringComparison.Ordinal) &&
        string.Equals(saved.Method, observation.Method.ToString(), StringComparison.Ordinal) && string.Equals(saved.ProviderAlgorithm, observation.ProviderAlgorithm, StringComparison.Ordinal) &&
        saved.DerivationVersion is null && saved.InputIds.Length == 0 &&
        string.Equals(saved.ExternalId, item.ExternalId, StringComparison.Ordinal) &&
        string.Equals(saved.ExternalRevision, item.Revision, StringComparison.Ordinal) && saved.ExternalRevisionOrder == item.RevisionOrder &&
        saved.WorkoutId == item.Provenance?.WorkoutId &&
        string.Equals(saved.NativeMetric, item.Provenance?.NativeMetric, StringComparison.Ordinal) &&
        string.Equals(saved.NativeUnit, item.Provenance?.NativeUnit, StringComparison.Ordinal) &&
        saved.NativeValue == item.Provenance?.NativeValue &&
        string.Equals(saved.MappingVersion, item.Provenance?.MappingVersion, StringComparison.Ordinal);
}
