using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Feedback numerico e testuale versionato, con scrittura atomica delle osservazioni.</summary>
public sealed class AthleteFeedbackStore(MoscoRunDbContext db, ScientificImportStore imports, TimeProvider clock)
{
    private const string Source = "athlete-feedback";

    private static readonly HashSet<string> Scores = new(StringComparer.Ordinal)
    {
        "rpe", "fatigue", "stressPerceived", "soreness", "pain", "sleepQuality", "hunger",
        "gastrointestinalDiscomfort", "motivation",
    };

    private static readonly HashSet<string> Quantities = new(StringComparer.Ordinal)
    {
        "energyIntake", "carbohydrateIntake", "proteinIntake", "fatIntake", "fluidIntake",
        "sodiumIntake", "caffeineIntake", "sleepDuration", "napDuration",
    };

    public async Task<int> SaveAsync(Guid ownerId, string entryId, string revision, DateTimeOffset time,
        IReadOnlyList<AthleteFeedbackValue> values, CancellationToken ct = default)
    {
        var result = await SaveEntryAsync(ownerId, new AthleteFeedbackEntry(entryId, revision, time, values), ct);
        return result.ObservationsInserted;
    }

    public async Task<AthleteFeedbackSaveResult> SaveEntryAsync(Guid ownerId, AthleteFeedbackEntry entry,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var now = Normalize(clock.GetUtcNow());
        ValidateEntry(ownerId, entry, now);
        var items = CreateItems(entry);
        var id = ScientificImportStore.Identity(new ScientificImportItem(Source, entry.EntryId, entry.Revision,
            "feedback-entry", "native", null, entry.Time, ObservationMethod.SelfReported, RevisionOrder: entry.RevisionOrder));
        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Usare uno scope dedicato.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(token);
                var result = await PersistAsync(ownerId, id, entry, items, token);
                await tx.CommitAsync(token);
                return result;
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }, ct);
    }

    private async Task<AthleteFeedbackSaveResult> PersistAsync(Guid ownerId, Guid id, AthleteFeedbackEntry entry,
        ScientificImportItem[] items, CancellationToken ct)
    {
        await ScientificWriteGate.AssertWritableAsync(db, ownerId, ct);
        if (entry.WorkoutId is { } workoutId && !await db.Workouts.AnyAsync(w => w.OwnerId == ownerId && w.Id == workoutId, ct))
        {
            throw new ArgumentException("Allenamento non autorizzato.", nameof(entry));
        }

        var entries = db.Set<AthleteFeedbackEntryRow>();
        var old = await entries.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, ct);
        var ids = items.Select(ScientificImportStore.Identity).Order().ToArray();
        if (old is not null && !Matches(old, entry, ids))
        {
            throw new InvalidOperationException("Revisione del feedback già presente con contenuto diverso.");
        }

        var count = items.Length == 0 ? 0 : await imports.ImportAsync(ownerId, items, ct);
        if (old is not null)
        {
            if (count != 0)
            {
                throw new InvalidOperationException("Feedback già presente con osservazioni mancanti.");
            }

            return new AthleteFeedbackSaveResult(false, 0);
        }

        var availableAt = Normalize(clock.GetUtcNow());
        if (ids.Length > 0)
        {
            var latest = await db.Set<ScientificObservationRow>().Where(o => o.OwnerId == ownerId && ids.Contains(o.Id))
                .MaxAsync(o => o.AvailableAt, ct);
            if (latest > availableAt)
            {
                availableAt = latest;
            }
        }

        entries.Add(new AthleteFeedbackEntryRow
        {
            OwnerId = ownerId,
            Id = id,
            EntryId = entry.EntryId,
            Revision = entry.Revision,
            RevisionOrder = entry.RevisionOrder,
            Time = Normalize(entry.Time),
            AvailableAt = availableAt,
            WorkoutId = entry.WorkoutId,
            Note = entry.Note,
            ObservationIds = ids,
        });
        await db.SaveChangesAsync(ct);
        return new AthleteFeedbackSaveResult(true, count);
    }

    private static bool Matches(AthleteFeedbackEntryRow old, AthleteFeedbackEntry entry, Guid[] ids) =>
        old.Time == Normalize(entry.Time) && old.WorkoutId == entry.WorkoutId && old.RevisionOrder == entry.RevisionOrder &&
        string.Equals(old.Note, entry.Note, StringComparison.Ordinal) && old.ObservationIds.SequenceEqual(ids);

    private static void ValidateEntry(Guid ownerId, AthleteFeedbackEntry entry, DateTimeOffset now)
    {
        if (ownerId == Guid.Empty || string.IsNullOrWhiteSpace(entry.EntryId) || entry.EntryId.Length > 256 ||
            string.IsNullOrWhiteSpace(entry.Revision) || entry.Revision.Length > 200 || entry.RevisionOrder < 0 ||
            entry.Time == default || Normalize(entry.Time) > now || entry.WorkoutId == Guid.Empty ||
            entry.Note?.Length > 4000 || entry.Values is null ||
            entry.Values.Count > ScientificImportStore.MaxBatchSize ||
            (entry.Values.Count == 0 && string.IsNullOrWhiteSpace(entry.Note)))
        {
            throw new ArgumentException("Feedback non valido.", nameof(entry));
        }
    }

    private static ScientificImportItem[] CreateItems(AthleteFeedbackEntry entry)
    {
        return entry.Values.Select(v =>
        {
            var completeness = string.Equals(v.Metric, ScientificFeedbackKeys.DailyCompleteMetric, StringComparison.Ordinal);
            if ((!completeness && !Scores.Contains(v.Metric) && !Quantities.Contains(v.Metric)) ||
                (completeness && (entry.WorkoutId is not null || v.Value is not (null or 0 or 1))) ||
                (v.Value is { } number && (!double.IsFinite(number) || number < 0 ||
                    (Scores.Contains(v.Metric) && number > 10))))
            {
                throw new ArgumentException("Feedback fuori catalogo o intervallo.", nameof(entry));
            }

            return new ScientificImportItem(Source, entry.EntryId, entry.Revision, v.Metric, v.Unit,
                v.Value, entry.Time, ObservationMethod.SelfReported, RevisionOrder: entry.RevisionOrder,
                Provenance: entry.WorkoutId is null ? null : new ScientificImportProvenance(WorkoutId: entry.WorkoutId));
        }).ToArray();
    }

    private static DateTimeOffset Normalize(DateTimeOffset time)
    {
        var ticks = time.UtcTicks;
        return new DateTimeOffset(ticks - (ticks % 10), TimeSpan.Zero);
    }
}
