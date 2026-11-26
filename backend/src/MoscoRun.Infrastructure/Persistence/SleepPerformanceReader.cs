using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class SleepPerformanceReader(MoscoRunDbContext db)
{
    public async Task<SleepPerformanceReport?> ReadAsync(Guid owner, string source, DateOnly last,
        int days, int lag, double distance, DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(source) || source.Length > Workout.SourceMaxLength ||
            !SleepPerformanceEngine.ValidWindow(last, days, lag, asOf, distance))
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var sleep = await new SleepDiaryReader(db).ReadCoreAsync(owner, last.AddDays(-lag), days, asOf, token);
            var first = last.AddDays(1 - days);
            var race = sleep is null ? null : await new AttestedRaceReader(db).ReadCoreAsync(owner, source,
                At(first), At(first.AddDays(1)), At(last.AddDays(1)), asOf, distance, token);
            var result = sleep is not null && race is not null
                ? SleepPerformanceEngine.Build(source, last, days, lag, asOf, distance, sleep.Nights, race.Evidence)
                : null;
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
