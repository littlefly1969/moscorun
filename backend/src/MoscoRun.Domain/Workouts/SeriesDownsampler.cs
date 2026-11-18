namespace MoscoRun.Domain.Workouts;

/// <summary>
/// Riduce una serie a un numero massimo di campioni per i grafici senza inventare valori: la serie è divisa in
/// intervalli di uguale numero di campioni e di ciascuno si conservano il minimo e il massimo reali, nell'ordine
/// in cui compaiono. Picchi e cadute (es. frequenza cardiaca massima) restano quindi visibili, cosa che una
/// media o un campionamento a passo fisso non garantiscono. Primo e ultimo campione sono sempre presenti.
/// </summary>
public static class SeriesDownsampler
{
    public const int MinPoints = 2;

    public static IReadOnlyList<SeriesSample> Downsample(IReadOnlyList<SeriesSample> samples, int maxPoints)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, MinPoints);
        if (samples.Count <= maxPoints)
        {
            return samples;
        }

        // Primo e ultimo sono fissi; il resto è diviso in intervalli che rendono al più due campioni ciascuno.
        var buckets = Math.Max(1, (maxPoints - 2) / 2);
        var inner = samples.Count - 2;
        var result = new List<SeriesSample>(maxPoints) { samples[0] };
        for (var b = 0; b < buckets; b++)
        {
            var start = 1 + (int)((long)inner * b / buckets);
            var end = 1 + (int)((long)inner * (b + 1) / buckets);
            if (start >= end)
            {
                continue;
            }

            var min = start;
            var max = start;
            for (var i = start + 1; i < end; i++)
            {
                if (samples[i].Value < samples[min].Value)
                {
                    min = i;
                }

                if (samples[i].Value > samples[max].Value)
                {
                    max = i;
                }
            }

            if (result.Count < maxPoints - 1)
            {
                result.Add(samples[Math.Min(min, max)]);
            }

            if (min != max && result.Count < maxPoints - 1)
            {
                result.Add(samples[Math.Max(min, max)]);
            }
        }

        result.Add(samples[^1]);
        return result;
    }
}
