namespace MoscoRun.Domain.Analysis;

/// <summary>Split temporale e atleta-out, senza riutilizzare label tardive nel training.</summary>
public static class LearningDataset
{
    public static LearningSplit? Split(
        IEnumerable<LearningExample> examples, DateTimeOffset cutoff, DateTimeOffset asOf, Guid? heldOutOwner = null)
    {
        ArgumentNullException.ThrowIfNull(examples);
        if (asOf <= cutoff || heldOutOwner == Guid.Empty)
        {
            return null;
        }

        var all = examples.ToArray();
        if (all.Length == 0 || all.Any(item => !Valid(item)) ||
            all.Select(item => (item.OwnerId, item.SessionId)).Distinct().Count() != all.Length ||
            all.Any(item => item.Features.Count != all[0].Features.Count))
        {
            return null;
        }

        var train = all.Where(item => item.TargetAvailableAt <= cutoff &&
            (!heldOutOwner.HasValue || item.OwnerId != heldOutOwner.Value))
            .OrderBy(item => item.PredictionAt).ToArray();
        var test = all.Where(item => item.PredictionAt > cutoff && item.TargetAvailableAt <= asOf &&
            (!heldOutOwner.HasValue || item.OwnerId == heldOutOwner.Value))
            .OrderBy(item => item.PredictionAt).ToArray();
        return train.Length > 0 && test.Length > 0
            ? new LearningSplit(Array.AsReadOnly(train), Array.AsReadOnly(test)) : null;
    }

    public static bool Valid(LearningExample item) =>
        item.OwnerId != Guid.Empty && item.SessionId != Guid.Empty &&
        item.FeaturesAvailableAt <= item.PredictionAt && item.PredictionAt < item.TargetAt &&
        item.TargetAt <= item.TargetAvailableAt && item.Features.Count is > 0 and <= 64 &&
        item.Features.All(double.IsFinite) && double.IsFinite(item.Target);
}
