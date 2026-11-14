namespace MoscoRun.Domain.Workouts;

/// <summary>Confronto fra una nuova ricezione e l'allenamento già salvato con la stessa chiave.</summary>
public enum WorkoutReplay
{
    /// <summary>Stesso contenuto: reinvio sicuro, nessuna modifica.</summary>
    Identical,

    /// <summary>Revisione più recente nella fonte: sostituisce la versione salvata.</summary>
    Newer,

    /// <summary>Contenuto diverso senza revisione più recente: rifiutato.</summary>
    Conflict,
}
