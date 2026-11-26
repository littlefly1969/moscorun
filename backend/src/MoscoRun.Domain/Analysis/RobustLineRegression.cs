namespace MoscoRun.Domain.Analysis;

/// <summary>Theil–Sen su coppie finite con ascisse distinte; limite esplicito per il costo quadratico.</summary>
public static class RobustLineRegression
{
    public static RobustLineFit? Fit(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Count < 3 || x.Count > 2048 || x.Count != y.Count || x.Any(value => !double.IsFinite(value)) ||
            y.Any(value => !double.IsFinite(value)))
        {
            return null;
        }

        var slopes = new List<double>();
        for (var left = 0; left < x.Count - 1; left++)
        {
            for (var right = left + 1; right < x.Count; right++)
            {
                var dx = x[right] - x[left];
                if (Math.Abs(dx) > 1e-12)
                {
                    var slope = (y[right] - y[left]) / dx;
                    if (!double.IsFinite(slope))
                    {
                        return null;
                    }

                    slopes.Add(slope);
                }
            }
        }

        var slopeMedian = SignalProcessing.Median(slopes);
        if (slopeMedian is not { } coefficient)
        {
            return null;
        }

        var intercept = SignalProcessing.Median(x.Zip(y, (abscissa, ordinate) => ordinate - coefficient * abscissa));
        if (intercept is not { } offset)
        {
            return null;
        }

        var residual = SignalProcessing.Median(x.Zip(y, (abscissa, ordinate) => Math.Abs(ordinate - (offset + coefficient * abscissa))));
        return residual.HasValue ? new RobustLineFit(offset, coefficient, residual.Value, x.Count) : null;
    }
}
