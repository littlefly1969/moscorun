namespace MoscoRun.Infrastructure.Persistence;

public sealed record InventoryObservationPage(DateTimeOffset AsOf, IReadOnlyList<InventoryObservation> Items, bool HasMore);
