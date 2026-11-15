using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace MoscoRun.Api.Features.Maps;

/// <summary>
/// Cartografia del cruscotto. Con <c>Provider</c> vuoto: Google se è presente una chiave, altrimenti nessuna
/// cartografia (il percorso è disegnato senza mappa di sfondo e nessuna richiesta esce dal server dell'utente).
/// </summary>
public sealed partial class MapsOptions
{
    public const string Section = "Maps";
    public const string Google = "google";
    public const string None = "none";

    public string? Provider { get; set; }

    /// <summary>Chiave Maps JavaScript API, limitata al dominio del server (referrer HTTP) nella console Google.</summary>
    public string? GoogleApiKey { get; set; }

    public string EffectiveProvider => string.IsNullOrWhiteSpace(Provider)
        ? (string.IsNullOrWhiteSpace(GoogleApiKey) ? None : Google)
        : Provider.Trim().ToLowerInvariant();

    public bool UsesGoogle => string.Equals(EffectiveProvider, Google, StringComparison.Ordinal);

    [GeneratedRegex("^AIza[0-9A-Za-z_-]{35}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    internal static partial Regex GoogleKeyPattern();
}

internal sealed class MapsOptionsValidation : IValidateOptions<MapsOptions>
{
    public ValidateOptionsResult Validate(string? name, MapsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.EffectiveProvider switch
        {
            MapsOptions.None => ValidateOptionsResult.Success,
            MapsOptions.Google when options.GoogleApiKey is { } key && MapsOptions.GoogleKeyPattern().IsMatch(key.Trim()) =>
                ValidateOptionsResult.Success,
            MapsOptions.Google => ValidateOptionsResult.Fail(
                "Maps__GoogleApiKey non valida: serve una chiave Maps JavaScript API (inizia con AIza, 39 caratteri)."),
            var other => ValidateOptionsResult.Fail($"Maps__Provider '{other}' non supportato: usare 'google' o 'none'."),
        };
    }
}
