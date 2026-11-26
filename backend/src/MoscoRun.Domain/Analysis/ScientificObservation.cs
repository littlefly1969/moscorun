namespace MoscoRun.Domain.Analysis;

/// <summary>Osservazione immutabile; null indica un valore non disponibile, non zero.</summary>
public sealed class ScientificObservation
{
    public ScientificObservation(
        Guid id,
        Guid ownerId,
        string metric,
        string unit,
        double? value,
        DateTimeOffset time,
        DateTimeOffset availableAt,
        string source,
        ObservationMethod method,
        string? providerAlgorithm = null,
        string? derivationVersion = null,
        IEnumerable<Guid>? inputIds = null)
    {
        ValidateMetadata(id, ownerId, metric, unit, source);

        if (time == default || availableAt < time)
        {
            throw new ArgumentException("Tempo di disponibilità non valido.", nameof(availableAt));
        }

        if (!ScientificMetricCatalog.AcceptsUnit(metric, unit))
        {
            throw new ArgumentException("Unità non conforme al catalogo.", nameof(unit));
        }

        if (value is { } number && !double.IsFinite(number))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        if (method == ObservationMethod.ProviderEstimated && string.IsNullOrWhiteSpace(providerAlgorithm))
        {
            throw new ArgumentException("Algoritmo del produttore obbligatorio (anche unknown dichiarato).", nameof(providerAlgorithm));
        }

        var inputs = inputIds?.ToArray() ?? [];
        if (inputs.Any(input => input == Guid.Empty || input == id) || inputs.Distinct().Count() != inputs.Length)
        {
            throw new ArgumentException("Identificativi di derivazione non validi.", nameof(inputIds));
        }

        if (method is ObservationMethod.Derived or ObservationMethod.ModelInferred &&
            (string.IsNullOrWhiteSpace(derivationVersion) || inputs.Length == 0))
        {
            throw new ArgumentException("Versione e ingressi obbligatori per le stime derivate.", nameof(derivationVersion));
        }

        Id = id;
        OwnerId = ownerId;
        Metric = metric;
        Unit = unit;
        Value = value;
        Time = time.ToUniversalTime();
        AvailableAt = availableAt.ToUniversalTime();
        Source = source;
        Method = method;
        ProviderAlgorithm = providerAlgorithm;
        DerivationVersion = derivationVersion;
        InputIds = Array.AsReadOnly(inputs);
    }

    private static void ValidateMetadata(Guid id, Guid ownerId, string metric, string unit, string source)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identità dell'osservazione obbligatoria.", nameof(id));
        }

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Identità dell'atleta obbligatoria.", nameof(ownerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (metric.Length > 200)
        {
            throw new ArgumentException("Metrica troppo lunga.", nameof(metric));
        }

        if (unit.Length > 200)
        {
            throw new ArgumentException("Unità troppo lunga.", nameof(unit));
        }

        if (source.Length > 200)
        {
            throw new ArgumentException("Sorgente troppo lunga.", nameof(source));
        }
    }

    public Guid Id { get; }

    public Guid OwnerId { get; }

    public string Metric { get; }

    public string Unit { get; }

    public double? Value { get; }

    public DateTimeOffset Time { get; }

    public DateTimeOffset AvailableAt { get; }

    public string Source { get; }

    public ObservationMethod Method { get; }

    public string? ProviderAlgorithm { get; }

    public string? DerivationVersion { get; }

    public IReadOnlyList<Guid> InputIds { get; }

    public bool IsMapped => ScientificMetricCatalog.IsKnown(Metric);

    public bool IsAvailableAt(DateTimeOffset asOf) => Time <= asOf && AvailableAt <= asOf;
}
