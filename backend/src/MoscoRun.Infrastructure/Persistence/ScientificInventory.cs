namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificInventory(DateTimeOffset AsOf, IReadOnlyList<ObservationInventoryGroup> Observations,
    IReadOnlyList<SignalInventoryGroup> Signals, IReadOnlyList<ScaleInventoryGroup> Scales,
    IReadOnlyList<InventoryDevice> Devices);
