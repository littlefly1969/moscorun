namespace MoscoRun.IntegrationTests;

/// <summary>
/// Test che avviano host aggiuntivi da <c>Program</c>: WebApplicationFactory intercetta la costruzione dell'host
/// con un listener globale, quindi questi test non girano in parallelo tra loro né con gli altri.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HostStartupGroup
{
    public const string Name = "Avvio host";
}
