namespace MoscoRun.Domain.Analysis;

/// <summary>Intervallo di durata nota con intensità nella stessa unità delle soglie; null è copertura mancante.</summary>
public sealed record IntensitySlice(double Minutes, double? Intensity);
