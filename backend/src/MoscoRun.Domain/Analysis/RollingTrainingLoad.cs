namespace MoscoRun.Domain.Analysis;

public static class RollingTrainingLoad
{
    public static RollingLoadSummary? Compare(
        IReadOnlyList<DailyTrainingLoad> loads, DateOnly last, DateTimeOffset asOf, int recentDays, int priorDays)
    {
        ArgumentNullException.ThrowIfNull(loads);
        if (recentDays <= 0 || priorDays <= 0 || recentDays > 366 || priorDays > 366 ||
            last.DayNumber < recentDays + priorDays - 1)
        {
            return null;
        }

        var first = last.AddDays(1 - recentDays - priorDays);
        var rows = loads.Where(item => item.Date >= first && item.Date <= last && item.AvailableAt <= asOf)
            .OrderBy(item => item.Date).ToArray();
        if (rows.Length != recentDays + priorDays)
        {
            return null;
        }

        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].Date != first.AddDays(index) || rows[index].Load is not { } load ||
                !double.IsFinite(load) || load < 0)
            {
                return null;
            }
        }

        var prior = rows.Take(priorDays).Average(item => item.Load.GetValueOrDefault());
        var recent = rows.Skip(priorDays).Average(item => item.Load.GetValueOrDefault());
        if (!double.IsFinite(prior) || !double.IsFinite(recent))
        {
            return null;
        }

        var ratio = recent / prior;
        return new RollingLoadSummary(recent, prior, prior > 0 && double.IsFinite(ratio) ? ratio : null);
    }
}
