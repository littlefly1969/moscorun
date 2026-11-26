using MoscoRun.Domain.Analysis;

namespace MoscoRun.Api.Features.Science;

/// <summary>Allenamento indicato con la sua identità naturale nella sorgente: il client non conosce gli ID del server.</summary>
public sealed record WorkoutReferenceDto(string? Source, string? SourceId);

/// <summary>Dato come lo espone la sorgente, prima della conversione all'unità canonica.</summary>
public sealed record NativeValueDto(string? Metric, string? Unit, double? Value, string? MappingVersion);

/// <summary>Checkpoint del connettore, avanzato nella stessa transazione dell'import.</summary>
public sealed record SyncPageDto(string? Stream, string? PreviousCursor, string? NextCursor);

public sealed record ObservationSampleDto(
    string? ExternalId,
    string? Revision,
    long? RevisionOrder,
    string? Metric,
    string? Unit,
    double? Value,
    DateTimeOffset Time,
    ObservationMethod Method,
    string? ProviderAlgorithm = null,
    WorkoutReferenceDto? Workout = null,
    NativeValueDto? Native = null);

public sealed record IngestObservationsRequest(string? Source, IReadOnlyList<ObservationSampleDto>? Samples, SyncPageDto? Page = null);

/// <summary>Esito di un lotto di osservazioni.</summary>
/// <param name="Received">Campioni nella richiesta.</param>
/// <param name="Imported">Revisioni nuove scritte; un reinvio identico vale zero.</param>
/// <param name="Cursor">Checkpoint salvato dopo l'import, se la richiesta ne indicava uno.</param>
public sealed record IngestObservationsResult(int Received, int Imported, string? Cursor);

public sealed record SyncCursorDto(string Source, string Stream, string? Cursor);

public sealed record SignalSampleDto(DateTimeOffset Time, double? Value);

public sealed record IngestSignalRequest(
    string? Source,
    string? ExternalId,
    string? Revision,
    long? RevisionOrder,
    string? Metric,
    string? Unit,
    ObservationMethod Method,
    IReadOnlyList<SignalSampleDto>? Samples,
    string? ProviderAlgorithm = null,
    WorkoutReferenceDto? Workout = null);

/// <summary>Esito dell'invio di un chunk di segnale.</summary>
/// <param name="Id">Identità del chunk (stabile per sorgente, externalId, revisione e metrica).</param>
/// <param name="SampleCount">Campioni salvati nel chunk.</param>
/// <param name="Created">false se il chunk era già presente con contenuto identico.</param>
public sealed record IngestSignalResult(Guid Id, int SampleCount, bool Created);
