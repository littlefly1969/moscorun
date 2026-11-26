using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class PersonalAdaptationReader(MoscoRunDbContext db)
{
    public async Task<PersonalAdaptationReport?> ReadAsync(Guid owner, string source, DateOnly first,
        DateOnly split, DateOnly until, DateTimeOffset asOf, double distance, CancellationToken ct = default)
    {
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(source) || source.Length > Workout.SourceMaxLength ||
            !PersonalAdaptationEngine.ValidWindow(first, split, until, asOf, distance))
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var history = await new DailySessionLoadReader(db).ReadHistoryCoreAsync(owner, first, until.AddDays(-1), asOf, token);
            var race = history is null ? null : await new AttestedRaceReader(db).ReadCoreAsync(owner, source,
                At(first), At(split), At(until), asOf, distance, token);
            var report = history is not null && race is not null
                ? PersonalAdaptationEngine.Build(owner, source, first, split, until, asOf, distance, history, race.Evidence)
                : null;
            await tx.CommitAsync(token);
            return report;
        }, ct);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
