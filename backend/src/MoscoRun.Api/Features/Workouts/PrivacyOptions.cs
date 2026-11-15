using Microsoft.Extensions.Options;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Api.Features.Workouts;

/// <summary>Zone private: <c>Privacy__RouteZones=lat,lon,raggio;…</c>. Vuoto (predefinito) = tracciato completo.</summary>
public sealed class PrivacyOptions
{
    public const string Section = "Privacy";

    public string? RouteZones { get; set; }

    private (string? Source, IReadOnlyList<PrivacyZone> Zones) _parsed = (null, []);

    /// <summary>Zone già validate all'avvio (<see cref="PrivacyOptionsValidation"/>), analizzate una volta sola.</summary>
    public IReadOnlyList<PrivacyZone> Zones
    {
        get
        {
            var parsed = _parsed;
            if (!string.Equals(parsed.Source, RouteZones, StringComparison.Ordinal))
            {
                parsed = (RouteZones, PrivacyZone.ParseList(RouteZones) is { IsSuccess: true } r ? r.Value : []);
                _parsed = parsed;
            }

            return parsed.Zones;
        }
    }
}

internal sealed class PrivacyOptionsValidation : IValidateOptions<PrivacyOptions>
{
    public ValidateOptionsResult Validate(string? name, PrivacyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var parsed = PrivacyZone.ParseList(options.RouteZones);
        return parsed.IsSuccess
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(parsed.Errors.Select(e => $"Privacy__RouteZones: {e.Message}"));
    }
}
