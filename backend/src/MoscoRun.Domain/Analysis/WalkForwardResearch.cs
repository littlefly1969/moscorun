namespace MoscoRun.Domain.Analysis;

/// <summary>Valida una baseline ridge contro la media del training, usando finestre disgiunte.</summary>
public static class WalkForwardResearch
{
    public static IReadOnlyList<FoldBenchmark>? Evaluate(
        IReadOnlyList<LearningExample> examples, IReadOnlyList<ResearchFold> folds, double penalty, Guid? heldOutOwner = null)
    {
        ArgumentNullException.ThrowIfNull(examples);
        ArgumentNullException.ThrowIfNull(folds);
        if (folds.Count == 0)
        {
            return null;
        }

        var results = new List<FoldBenchmark>(folds.Count);
        DateTimeOffset? previousEnd = null;
        foreach (var fold in folds)
        {
            if (fold.AsOf <= fold.Cutoff || previousEnd > fold.Cutoff)
            {
                return null;
            }

            var split = LearningDataset.Split(examples, fold.Cutoff, fold.AsOf, heldOutOwner);
            if (split is null)
            {
                return null;
            }

            var report = EvaluateFold(split, fold, penalty);
            if (report is null)
            {
                return null;
            }

            results.Add(new FoldBenchmark(fold, report));
            previousEnd = fold.AsOf;
        }

        return results.AsReadOnly();
    }

    private static BenchmarkReport? EvaluateFold(LearningSplit split, ResearchFold fold, double penalty)
    {
        var model = RidgeRegression.Fit(split.Training, fold.Cutoff, penalty);
        if (model is null)
        {
            return null;
        }

        var baseline = split.Training.Average(row => row.Target);
        var predictions = new List<BenchmarkPrediction>(split.Test.Count);
        foreach (var item in split.Test)
        {
            var prediction = model.Predict(item.Features);
            if (prediction is not { } value)
            {
                return null;
            }

            predictions.Add(new BenchmarkPrediction(item.OwnerId, item.SessionId, item.PredictionAt,
                item.TargetAt, item.TargetAvailableAt, item.Target, value, baseline));
        }

        return ResearchBenchmark.Evaluate(predictions, fold.Cutoff, fold.AsOf);
    }
}
