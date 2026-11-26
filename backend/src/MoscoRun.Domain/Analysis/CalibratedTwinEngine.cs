namespace MoscoRun.Domain.Analysis;

/// <summary>Scenario FC condizionale: il trasferimento della tau di recupero allo sforzo non è una validazione.</summary>
public static class CalibratedTwinEngine
{
    public const string MethodVersion = "calibrated-conditional-hr-twin-v1";

    public static bool Valid(CalibratedTwinRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.RecoveryProtocol is { } protocol && protocol.IsValid() &&
            Positive(request.RestingBpm) && Positive(request.MaximumBpm) && request.MaximumBpm > request.RestingBpm &&
            InRange(request.InitialBpm, request) && Positive(request.MaximumFitRmseBpm) &&
            request.Segments is { Count: >= 1 and <= 100 } &&
            request.Segments.All(item => item is not null && Positive(item.DurationSeconds) &&
                double.IsFinite(item.ExternalSpeedMetersPerSecond) && item.ExternalSpeedMetersPerSecond >= 0 &&
                InRange(item.EquilibriumHeartRateBpm, request)) &&
            request.Segments.Sum(item => item.DurationSeconds) <= 7200;
    }

    public static CalibratedTwinReport? Build(CalibratedTwinRequest request,
        WorkoutCardiacRecoveryReport calibration, DateTimeOffset cutoff, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        if (!Valid(request) || calibration.Protocol != request.RecoveryProtocol ||
            calibration.AsOf != asOf || cutoff > asOf)
        {
            return null;
        }

        if (!request.TransferAssumptionConfirmed)
        {
            return Empty(request, calibration, asOf, "unconfirmed-transfer");
        }

        var fit = calibration.Fit;
        if (fit is null || !request.RecoveryProtocol.Confirmed || fit.TimeConstantSeconds is <= 1 or >= 300 ||
            fit.RootMeanSquaredErrorBpm > request.MaximumFitRmseBpm ||
            !double.IsFinite(fit.RootMeanSquaredErrorBpm) ||
            request.RecoveryProtocol.EquilibriumBpm is not { } baseline || !InRange(baseline, request) ||
            calibration.Points.Any(point => point.HeartRateBpm is { } bpm && !InRange(bpm, request)))
        {
            return Empty(request, calibration, asOf, "insufficient-calibration");
        }

        var parameters = new TwinParameters(calibration.MethodVersion, cutoff,
            fit.TimeConstantSeconds, request.RestingBpm, request.MaximumBpm);
        var simulation = AthleteDigitalTwin.Simulate(request.Segments, request.InitialBpm, parameters, asOf);
        return new CalibratedTwinReport(MethodVersion, asOf, simulation is null ? "invalid-simulation" : "conditional-simulation",
            calibration, request, parameters, simulation ?? []);
    }

    private static CalibratedTwinReport Empty(CalibratedTwinRequest request, WorkoutCardiacRecoveryReport calibration,
        DateTimeOffset asOf, string status) => new(MethodVersion, asOf, status, calibration, request, null, []);

    private static bool InRange(double bpm, CalibratedTwinRequest request) =>
        double.IsFinite(bpm) && bpm >= request.RestingBpm && bpm <= request.MaximumBpm;

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
