namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Pagina immutabile e cursore opaco non contenente credenziali.</summary>
public sealed record ScientificSyncPage(string Source, string Stream, string? PreviousCursor, string NextCursor,
    IReadOnlyList<ScientificImportItem> Items);
