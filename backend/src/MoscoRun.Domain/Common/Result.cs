namespace MoscoRun.Domain.Common;

/// <summary>Esito di un'operazione di dominio senza eccezioni per i casi attesi.</summary>
public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<DomainError> errors)
    {
        _value = value;
        Errors = errors;
    }

    public IReadOnlyList<DomainError> Errors { get; }

    public bool IsSuccess => Errors.Count == 0;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Il risultato contiene errori: valore non disponibile.");

    public static Result<T> Success(T value) => new(value, []);

    public static Result<T> Failure(IReadOnlyList<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("Un fallimento richiede almeno un errore.", nameof(errors));
        }

        return new(default, errors);
    }
}
