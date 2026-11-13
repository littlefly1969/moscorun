using System.Globalization;

namespace MoscoRun.Api.Commands;

/// <summary>
/// Healthcheck del container (l'immagine chiseled non ha curl né shell).
/// <c>healthcheck</c> verifica la liveness del processo (HEALTHCHECK Docker, nessuna dipendenza esterna);
/// <c>healthcheck ready</c> verifica anche il database ed è il gate di aggiornamento e rollback.
/// </summary>
internal static class HealthcheckCommand
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        var ready = args is [_, "ready", ..];
        var path = ready ? "/health/ready" : "/health/live";
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(ready ? 10 : 3) };
        try
        {
            using var response = await client.GetAsync(new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}{path}")));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }
}
