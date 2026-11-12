using System.ComponentModel.DataAnnotations;
using MoscoRun.Domain.Measurements;

namespace MoscoRun.Api.Features.Weighings;

/// <summary>Pesata definitiva inviata dall'agente della bilancia.</summary>
/// <param name="SourceEventId">Identificativo stabile generato dall'agente; reinviare lo stesso valore è sicuro.</param>
/// <param name="MeasuredAt">Istante della misura (ISO 8601 con offset).</param>
/// <param name="WeightKg">Peso in kg.</param>
/// <param name="ImpedanceOhm">Impedenza della stessa misura, se disponibile.</param>
public sealed record IngestWeighingRequest(
    [property: Required] string? SourceEventId,
    [property: Required] DateTimeOffset? MeasuredAt,
    [property: Required] decimal? WeightKg,
    int? ImpedanceOhm);

public sealed record WeighingDto(
    Guid Id,
    Guid DeviceId,
    string SourceEventId,
    DateTimeOffset MeasuredAt,
    decimal WeightKg,
    int? ImpedanceOhm,
    AttributionStatus Attribution,
    DateTimeOffset ReceivedAt)
{
    public static WeighingDto From(Weighing w) =>
        new(w.Id, w.DeviceId, w.SourceEventId, w.MeasuredAt, w.WeightKg, w.ImpedanceOhm, w.Attribution, w.ReceivedAt);
}

public sealed record WeighingList(IReadOnlyList<WeighingDto> Items, DateTimeOffset From, DateTimeOffset To);

public sealed record UpdateAttributionRequest([property: Required] AttributionStatus? Attribution);
