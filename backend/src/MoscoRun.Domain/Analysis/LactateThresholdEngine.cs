namespace MoscoRun.Domain.Analysis;

/// <summary>Interpolazione del primo attraversamento di una concentrazione di lattato definita dal protocollo.</summary>
public static class LactateThresholdEngine
{
    public static LactateThresholdEstimate? Interpolate(
        IReadOnlyList<LactateStage> stages, double targetMillimolesPerLiter, bool protocolAttested)
    {
        ArgumentNullException.ThrowIfNull(stages);
        if (!protocolAttested || stages.Count < 3 || !Positive(targetMillimolesPerLiter) ||
            stages.Any(stage => !Positive(stage.SpeedMetersPerSecond) || !Positive(stage.LactateMillimolesPerLiter) ||
                !Positive(stage.HeartRateBpm)))
        {
            return null;
        }

        for (var index = 1; index < stages.Count; index++)
        {
            if (stages[index].SpeedMetersPerSecond <= stages[index - 1].SpeedMetersPerSecond)
            {
                return null;
            }
        }

        if (stages[0].LactateMillimolesPerLiter >= targetMillimolesPerLiter)
        {
            return null;
        }

        for (var index = 1; index < stages.Count; index++)
        {
            var first = stages[index - 1];
            var last = stages[index];
            if (first.LactateMillimolesPerLiter < targetMillimolesPerLiter &&
                last.LactateMillimolesPerLiter >= targetMillimolesPerLiter)
            {
                var ratio = (targetMillimolesPerLiter - first.LactateMillimolesPerLiter) /
                    (last.LactateMillimolesPerLiter - first.LactateMillimolesPerLiter);
                return new LactateThresholdEstimate(first.SpeedMetersPerSecond + ratio * (last.SpeedMetersPerSecond - first.SpeedMetersPerSecond),
                    first.HeartRateBpm + ratio * (last.HeartRateBpm - first.HeartRateBpm), targetMillimolesPerLiter);
            }
        }

        return null;
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
