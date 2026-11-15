namespace MoscoRun.Domain.Workouts;

/// <summary>Grandezze registrate durante l'allenamento. L'unità è fissata dalla grandezza.</summary>
public enum SeriesMetric
{
    /// <summary>Frequenza cardiaca, battiti al minuto.</summary>
    HeartRate = 0,

    /// <summary>Velocità, metri al secondo.</summary>
    Speed = 1,

    /// <summary>Cadenza, passi (o pedalate) al minuto.</summary>
    Cadence = 2,

    /// <summary>Potenza, watt.</summary>
    Power = 3,
}
