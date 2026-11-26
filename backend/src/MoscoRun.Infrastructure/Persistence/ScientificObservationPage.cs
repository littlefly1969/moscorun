using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificObservationPage(IReadOnlyList<ScientificObservation> Items, int? NextOffset);
