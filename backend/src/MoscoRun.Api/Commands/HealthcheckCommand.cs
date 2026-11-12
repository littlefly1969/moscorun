using System.Globalization;

namespace MoscoRun.Api.Commands;

/// <summary>Healthcheck del container: l'immagine chiseled non ha curl né shell.</summary>
internal static class HealthcheckCommand
{
    public static async Task<int> RunAsync()
    {
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await client.GetAsync(new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/health/live")));
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
