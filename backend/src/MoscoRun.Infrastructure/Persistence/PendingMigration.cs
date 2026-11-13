namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Una migrazione in attesa e le operazioni che la rendono incompatibile con la versione precedente.</summary>
public sealed record PendingMigration(string Id, bool IsContract, IReadOnlyList<string> BreakingOperations);
