namespace MoscoRun.Domain.Common;

/// <summary>Errore di dominio con codice stabile, usabile dalle API come ProblemDetails.</summary>
/// <param name="Code">Codice stabile, es. <c>weighing.weight.out_of_range</c>.</param>
/// <param name="Message">Messaggio leggibile in italiano.</param>
/// <param name="Field">Campo della richiesta a cui si riferisce, se applicabile.</param>
public sealed record DomainError(string Code, string Message, string? Field = null);
