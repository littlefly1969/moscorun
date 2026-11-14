namespace MoscoRun.Infrastructure.Persistence;

/// <summary>
/// Segna una migrazione "contract": rimuove o modifica struttura che la versione precedente dell'applicazione
/// potrebbe ancora usare. Va pubblicata solo in una release successiva a quella che ha smesso di usarla
/// e viene applicata soltanto con consenso esplicito (<c>migrate --allow-contract</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ContractMigrationAttribute(string reason) : Attribute
{
    /// <summary>Perché la modifica è sicura: quale release ha smesso di usare la struttura rimossa.</summary>
    public string Reason { get; } = reason;
}
