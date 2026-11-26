namespace MoscoRun.Domain.Analysis;

/// <summary>Baseline lineare L2 con standardizzazione train-only e soluzione delle equazioni normali.</summary>
public static class RidgeRegression
{
    public const string MethodVersion = "ridge-standardized-v1";

    public static RidgeModel? Fit(IReadOnlyList<LearningExample> training, DateTimeOffset cutoff, double penalty)
    {
        ArgumentNullException.ThrowIfNull(training);
        if (training.Count < 3 || !double.IsFinite(penalty) || penalty <= 0 ||
            training.Any(item => !LearningDataset.Valid(item) || item.TargetAvailableAt > cutoff) ||
            training.Select(item => (item.OwnerId, item.SessionId)).Distinct().Count() != training.Count)
        {
            return null;
        }

        var dimensions = training[0].Features.Count;
        if (training.Any(item => item.Features.Count != dimensions))
        {
            return null;
        }

        var means = new double[dimensions];
        var scales = new double[dimensions];
        for (var column = 0; column < dimensions; column++)
        {
            means[column] = training.Average(item => item.Features[column]);
            var scale = Math.Sqrt(training.Average(item => Math.Pow(item.Features[column] - means[column], 2)));
            if (!double.IsFinite(means[column]) || !double.IsFinite(scale))
            {
                return null;
            }

            scales[column] = scale > 0 ? scale : 1;
        }

        var intercept = training.Average(item => item.Target);
        if (!double.IsFinite(intercept))
        {
            return null;
        }

        var matrix = BuildSystem(training, means, scales, intercept, penalty);
        var weights = Solve(matrix);
        return weights is not null ? new RidgeModel(means, scales, weights, intercept) : null;
    }

    private static double[][] BuildSystem(
        IReadOnlyList<LearningExample> training, double[] means, double[] scales, double intercept, double penalty)
    {
        var size = means.Length;
        var matrix = Enumerable.Range(0, size).Select(_ => new double[size + 1]).ToArray();
        foreach (var item in training)
        {
            var normalized = item.Features.Select((value, index) => (value - means[index]) / scales[index]).ToArray();
            for (var row = 0; row < size; row++)
            {
                for (var column = 0; column < size; column++)
                {
                    matrix[row][column] += normalized[row] * normalized[column] / training.Count;
                }

                matrix[row][size] += normalized[row] * (item.Target - intercept) / training.Count;
            }
        }

        for (var index = 0; index < size; index++)
        {
            matrix[index][index] += penalty;
        }

        return matrix;
    }

    private static double[]? Solve(double[][] matrix)
    {
        var size = matrix.Length;
        for (var index = 0; index < size; index++)
        {
            var pivot = index;
            for (var row = index + 1; row < size; row++)
            {
                if (Math.Abs(matrix[row][index]) > Math.Abs(matrix[pivot][index]))
                {
                    pivot = row;
                }
            }

            var divisor = matrix[pivot][index];
            if (!double.IsFinite(divisor) || Math.Abs(divisor) < 1e-12)
            {
                return null;
            }

            (matrix[index], matrix[pivot]) = (matrix[pivot], matrix[index]);
            for (var column = index; column <= size; column++)
            {
                matrix[index][column] /= divisor;
            }

            for (var row = 0; row < size; row++)
            {
                if (row == index)
                {
                    continue;
                }

                var factor = matrix[row][index];
                for (var column = index; column <= size; column++)
                {
                    matrix[row][column] -= factor * matrix[index][column];
                }
            }
        }

        var solution = matrix.Select(row => row[size]).ToArray();
        return solution.All(double.IsFinite) ? solution : null;
    }
}
