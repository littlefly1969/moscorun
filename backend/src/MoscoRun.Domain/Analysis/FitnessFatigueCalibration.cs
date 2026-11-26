namespace MoscoRun.Domain.Analysis;

/// <summary>Selezione su griglia esplicita di parametri, con intercetta appresa e replay prima di ogni esito.</summary>
public static class FitnessFatigueCalibration
{
    public const string MethodVersion = "fitness-fatigue-grid-calibration-v1";

    public static FitnessFatigueFit? Fit(
        Guid ownerId, IReadOnlyList<DailyTrainingLoad> loads, IEnumerable<FitnessFatigueCalibrationPoint> points,
        DateOnly firstDay, DateTimeOffset cutoff, FitnessFatigueState initial,
        IReadOnlyList<FitnessFatigueParameters> candidates)
    {
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(candidates);
        if (ownerId == Guid.Empty || firstDay == DateOnly.MinValue || initial.Date != firstDay.AddDays(-1) ||
            candidates.Count is < 1 or > 4096)
        {
            return null;
        }

        var rows = points.Where(point => point.OwnerId == ownerId && point.Time <= cutoff &&
            point.AvailableAt <= cutoff).OrderBy(point => point.Time).ToArray();
        if (rows.Length < 6 || rows.Any(point => point.ObservationId == Guid.Empty ||
                point.AvailableAt < point.Time || !double.IsFinite(point.Response)) ||
            rows.Select(point => point.ObservationId).Distinct().Count() != rows.Length ||
            rows.Select(point => DateOnly.FromDateTime(point.Time.UtcDateTime)).Distinct().Count() != rows.Length)
        {
            return null;
        }

        var aligned = AlignLoads(loads, rows, firstDay, cutoff);
        if (aligned is not { } input)
        {
            return null;
        }

        FitnessFatigueFit? best = null;
        foreach (var parameters in candidates)
        {
            var fit = EvaluateCandidate(ownerId, input.Daily, rows, input.RowIndices, firstDay, cutoff, initial, parameters);
            if (fit is not null && (best is null || fit.TrainingRmse < best.TrainingRmse))
            {
                best = fit;
            }
        }

        return best;
    }

    private static (DailyTrainingLoad[] Daily, int[] RowIndices)? AlignLoads(
        IReadOnlyList<DailyTrainingLoad> loads, FitnessFatigueCalibrationPoint[] rows,
        DateOnly firstDay, DateTimeOffset cutoff)
    {
        var lastMeasurementDay = DateOnly.FromDateTime(rows[^1].Time.UtcDateTime);
        if (DateOnly.FromDateTime(rows[0].Time.UtcDateTime) <= firstDay ||
            lastMeasurementDay.DayNumber - firstDay.DayNumber > 3660)
        {
            return null;
        }

        var daily = loads.Where(load => load.Date >= firstDay && load.Date < lastMeasurementDay &&
            load.AvailableAt <= cutoff).OrderBy(load => load.Date).ToArray();
        if (daily.Length != lastMeasurementDay.DayNumber - firstDay.DayNumber)
        {
            return null;
        }

        // Il carico di un giorno è applicato a fine giornata UTC; nessun dato del giorno dell'esito.
        var rowIndices = new int[rows.Length];
        var latestAvailability = DateTimeOffset.MinValue;
        var rowIndex = 0;
        for (var index = 0; index < daily.Length; index++)
        {
            var load = daily[index];
            if (load.Date != firstDay.AddDays(index) || load.Load is not { } value ||
                !double.IsFinite(value) || value < 0)
            {
                return null;
            }

            latestAvailability = load.AvailableAt > latestAvailability ? load.AvailableAt : latestAvailability;
            if (rowIndex < rows.Length && DateOnly.FromDateTime(rows[rowIndex].Time.UtcDateTime).DayNumber ==
                load.Date.DayNumber + 1)
            {
                if (latestAvailability > rows[rowIndex].Time)
                {
                    return null;
                }

                rowIndices[rowIndex++] = index;
            }
        }

        if (rowIndex != rows.Length)
        {
            return null;
        }

        return (daily, rowIndices);
    }

    private static FitnessFatigueFit? EvaluateCandidate(
        Guid ownerId, DailyTrainingLoad[] daily, FitnessFatigueCalibrationPoint[] rows, int[] rowIndices,
        DateOnly firstDay, DateTimeOffset cutoff, FitnessFatigueState initial, FitnessFatigueParameters parameters)
    {
        var state = initial;
        var residuals = new double[rows.Length];
        var rowIndex = 0;
        for (var index = 0; index < daily.Length; index++)
        {
            var item = daily[index];
            var next = FitnessFatigueEngine.Replay([item], item.Date, item.Date, cutoff, parameters, state);
            if (next is null)
            {
                return null;
            }

            state = next;
            if (rowIndex < rows.Length && rowIndices[rowIndex] == index)
            {
                residuals[rowIndex] = rows[rowIndex].Response - state.NetResponse;
                rowIndex++;
            }
        }

        var offset = residuals.Average();
        var rmse = Math.Sqrt(residuals.Average(value => Math.Pow(value - offset, 2)));
        return double.IsFinite(offset) && double.IsFinite(rmse)
            ? new FitnessFatigueFit(ownerId, firstDay, cutoff, initial, parameters, offset, rmse, rows.Length)
            : null;
    }
}
