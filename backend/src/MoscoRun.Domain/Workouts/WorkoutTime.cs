namespace MoscoRun.Domain.Workouts;

/// <summary>
/// Risoluzione temporale unica di un allenamento. Estremi, traccia GPS e serie sono tutti al millisecondo
/// (la risoluzione dei dispositivi e di <see cref="TrackCodec"/> / <see cref="SeriesCodec"/>): così un campione
/// registrato nello stesso millisecondo dell'inizio o della fine appartiene sempre alla finestra dell'allenamento,
/// anche quando arriva da un flusso con precisione maggiore (segnali scientifici a 100 ns).
/// </summary>
public static class WorkoutTime
{
    public static readonly TimeSpan Resolution = TimeSpan.FromMilliseconds(1);

    /// <summary>Tronca al millisecondo, in UTC.</summary>
    public static DateTimeOffset Floor(DateTimeOffset value) =>
        DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());

    /// <summary>Primo istante (in tick UTC) che cade oltre il millisecondo di <paramref name="end"/>.</summary>
    public static long ExclusiveEndTicks(DateTimeOffset end) => Floor(end).UtcTicks + TimeSpan.TicksPerMillisecond;

}
