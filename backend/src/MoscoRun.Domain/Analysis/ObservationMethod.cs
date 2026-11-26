namespace MoscoRun.Domain.Analysis;

/// <summary>Provenienza epistemica del valore, distinta dalla sorgente tecnica.</summary>
public enum ObservationMethod
{
    Measured,
    ProviderEstimated,
    SelfReported,
    Derived,
    ModelInferred,
    ProviderReported,
}
