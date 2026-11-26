namespace MoscoRun.Domain.Analysis;

/// <summary>DFA lineare short-term su intervalli NN consecutivi già validati; nessuna soglia di intensità universale.</summary>
public static class DfaAlpha1Engine
{
    public const string MethodVersion = "dfa1-scales4-16-forward-backward-v1";

    public static DfaAlpha1Result? Estimate(IReadOnlyList<double> intervalsMilliseconds, bool artifactScreened)
    {
        ArgumentNullException.ThrowIfNull(intervalsMilliseconds);
        if (!artifactScreened || intervalsMilliseconds.Count < 128 || intervalsMilliseconds.Count > 100000 ||
            intervalsMilliseconds.Any(value => !double.IsFinite(value) || value < 250 || value > 2500))
        {
            return null;
        }

        var mean = intervalsMilliseconds.Average();
        var profile = new double[intervalsMilliseconds.Count];
        double integrated = 0;
        for (var index = 0; index < profile.Length; index++)
        {
            integrated += intervalsMilliseconds[index] - mean;
            profile[index] = integrated;
        }

        var logScales = new List<double>();
        var logFluctuations = new List<double>();
        for (var scale = 4; scale <= 16; scale++)
        {
            var fluctuation = Fluctuation(profile, scale);
            if (!double.IsFinite(fluctuation) || fluctuation <= 0)
            {
                return null;
            }

            logScales.Add(Math.Log(scale));
            logFluctuations.Add(Math.Log(fluctuation));
        }

        var xMean = logScales.Average();
        var yMean = logFluctuations.Average();
        var covariance = logScales.Zip(logFluctuations, (x, y) => (x - xMean) * (y - yMean)).Sum();
        var variance = logScales.Sum(x => Math.Pow(x - xMean, 2));
        var alpha = covariance / variance;
        return double.IsFinite(alpha) ? new DfaAlpha1Result(alpha, profile.Length, logScales.Count) : null;
    }

    private static double Fluctuation(double[] profile, int scale)
    {
        var blocks = profile.Length / scale;
        double residuals = 0;
        for (var block = 0; block < blocks; block++)
        {
            residuals += ResidualSum(profile, block * scale, scale);
            residuals += ResidualSum(profile, profile.Length - (block + 1) * scale, scale);
        }

        return Math.Sqrt(residuals / (2.0 * blocks * scale));
    }

    private static double ResidualSum(double[] profile, int start, int scale)
    {
        var xMean = (scale - 1) / 2.0;
        double yMean = 0;
        for (var index = 0; index < scale; index++)
        {
            yMean += profile[start + index] / scale;
        }

        double covariance = 0;
        double variance = 0;
        for (var index = 0; index < scale; index++)
        {
            covariance += (index - xMean) * (profile[start + index] - yMean);
            variance += Math.Pow(index - xMean, 2);
        }

        var slope = covariance / variance;
        double sum = 0;
        for (var index = 0; index < scale; index++)
        {
            sum += Math.Pow(profile[start + index] - yMean - slope * (index - xMean), 2);
        }

        return sum;
    }
}
