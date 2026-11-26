using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Persistenza di feedback numerici dichiarati dall'atleta, non misurazioni cliniche.</summary>
public sealed class AthleteFeedbackStore(ScientificImportStore store)
{
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

    public Task<int> SaveAsync(Guid ownerId, string entryId, string revision, DateTimeOffset time,
        IReadOnlyList<AthleteFeedbackValue> values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > ScientificImportStore.MaxBatchSize)
        {
            throw new ArgumentException("Batch troppo grande.", nameof(values));
        }

        var items = values.Select(v =>
        {
            if ((!Scores.Contains(v.Metric) && !Quantities.Contains(v.Metric)) ||
                (v.Value is { } number && (!double.IsFinite(number) || number < 0 ||
                    (Scores.Contains(v.Metric) && number > 10))))
            {
                throw new ArgumentException("Feedback fuori catalogo o intervallo.", nameof(values));
            }

            return new ScientificImportItem("athlete-feedback", entryId, revision, v.Metric, v.Unit,
                v.Value, time, ObservationMethod.SelfReported);
        }).ToArray();
        return store.ImportAsync(ownerId, items, ct);
    }
}
