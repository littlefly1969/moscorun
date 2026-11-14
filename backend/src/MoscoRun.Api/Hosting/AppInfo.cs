using System.Reflection;

namespace MoscoRun.Api.Hosting;

/// <summary>Versione dell'applicazione impressa in build (tag di release e commit).</summary>
public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0-unknown";

    /// <summary>Vero quando l'host è avviato in build solo per generare il contratto OpenAPI.</summary>
    public static bool IsGeneratingOpenApiDocument { get; } =
        string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.Ordinal);
}
