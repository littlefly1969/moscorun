using System.Runtime.InteropServices;

namespace MoscoRun.Domain.Workouts;

/// <summary>Campione di una serie temporale dell'allenamento (frequenza cardiaca, velocità…).</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SeriesSample(DateTimeOffset Time, double Value);
