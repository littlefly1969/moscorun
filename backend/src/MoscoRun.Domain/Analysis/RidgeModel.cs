namespace MoscoRun.Domain.Analysis;

/// <summary>Regressione standardizzata. Media e scala calcolate esclusivamente sul training.</summary>
public sealed class RidgeModel
{
    private readonly double[] _means;
    private readonly double[] _scales;
    private readonly double[] _weights;

    internal RidgeModel(double[] means, double[] scales, double[] weights, double intercept)
    {
        _means = means;
        _scales = scales;
        _weights = weights;
        Intercept = intercept;
    }

    public double Intercept { get; }

    public int FeatureCount => _weights.Length;

    public double? Predict(IReadOnlyList<double> features)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (features.Count != FeatureCount || features.Any(value => !double.IsFinite(value)))
        {
            return null;
        }

        var prediction = Intercept;
        for (var index = 0; index < FeatureCount; index++)
        {
            prediction += (features[index] - _means[index]) / _scales[index] * _weights[index];
        }

        return double.IsFinite(prediction) ? prediction : null;
    }
}
