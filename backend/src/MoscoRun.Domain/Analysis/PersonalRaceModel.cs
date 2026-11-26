namespace MoscoRun.Domain.Analysis;

public sealed record PersonalRaceModel(
    Guid OwnerId, string Protocol, DateTimeOffset TrainingStart, DateTimeOffset Cutoff,
    double MinimumDistanceMeters, double MaximumDistanceMeters, double ReferenceDistanceMeters,
    double ReferenceSeconds, double Exponent, double MedianAbsoluteLogResidual, int Count)
{
    public RacePrediction? Predict(Guid ownerId, string protocol, DateTimeOffset issuedAt, double distanceMeters)
    {
        if (ownerId == Guid.Empty || ownerId != OwnerId || !string.Equals(protocol, Protocol, StringComparison.Ordinal) ||
            issuedAt < Cutoff || !double.IsFinite(distanceMeters) ||
            distanceMeters < MinimumDistanceMeters || distanceMeters > MaximumDistanceMeters)
        {
            return null;
        }

        var prediction = RacePredictionEngine.Riegel(ReferenceDistanceMeters, ReferenceSeconds, distanceMeters, Exponent);
        return prediction is null ? null : prediction with { MethodVersion = PersonalRaceCalibration.MethodVersion };
    }
}
