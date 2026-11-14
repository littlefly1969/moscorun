using System.Runtime.InteropServices;

namespace MoscoRun.Domain.Workouts;

/// <summary>Punto GPS registrato durante un allenamento.</summary>
/// <param name="Time">Istante del rilevamento.</param>
/// <param name="Latitude">Latitudine WGS84 in gradi.</param>
/// <param name="Longitude">Longitudine WGS84 in gradi.</param>
/// <param name="AltitudeM">Altitudine in metri, se rilevata.</param>
/// <param name="AccuracyM">Precisione orizzontale in metri, se dichiarata dalla fonte.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TrackPoint(
    DateTimeOffset Time,
    double Latitude,
    double Longitude,
    double? AltitudeM = null,
    double? AccuracyM = null);
