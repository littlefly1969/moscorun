namespace MoscoRun.Domain.Measurements;

/// <summary>Quanto è certa l'associazione della pesata alla persona.</summary>
public enum AttributionStatus
{
    /// <summary>Attribuita al proprietario del dispositivo, non verificata.</summary>
    Assumed = 0,

    /// <summary>Confermata dalla persona.</summary>
    Confirmed = 1,

    /// <summary>Esclusa dalla persona (es. bilancia usata da altri).</summary>
    Rejected = 2,
}
