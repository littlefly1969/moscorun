namespace MoscoRun.Domain.Analysis;

public sealed record FitnessFatigueFit(
    Guid OwnerId, DateOnly FirstDay, DateTimeOffset Cutoff, FitnessFatigueState Initial,
    FitnessFatigueParameters Parameters, double Offset, double TrainingRmse, int Count)
{
    public double? Predict(Guid ownerId, IReadOnlyList<DailyTrainingLoad> loads, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(loads);
        if (ownerId != OwnerId || ownerId == Guid.Empty || issuedAt < Cutoff)
        {
            return null;
        }

        var day = DateOnly.FromDateTime(issuedAt.UtcDateTime);
        if (day <= FirstDay)
        {
            return null;
        }

        var state = FitnessFatigueEngine.Replay(loads, FirstDay, day.AddDays(-1), issuedAt, Parameters, Initial);
        if (state is null)
        {
            return null;
        }

        var result = Offset + state.NetResponse;
        return double.IsFinite(result) ? result : null;
    }
}
