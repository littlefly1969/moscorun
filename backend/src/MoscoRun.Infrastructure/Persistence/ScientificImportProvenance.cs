namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Traccia della normalizzazione e del contesto autorizzato, distinta dal valore canonico.</summary>
public sealed record ScientificImportProvenance(Guid? DeviceId = null, Guid? WorkoutId = null,
    string? NativeMetric = null, string? NativeUnit = null, double? NativeValue = null, string? MappingVersion = null);
