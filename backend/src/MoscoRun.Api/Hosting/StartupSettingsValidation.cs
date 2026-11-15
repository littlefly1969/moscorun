using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MoscoRun.Infrastructure;
using Npgsql;

namespace MoscoRun.Api.Hosting;

/// <summary>Configurazione obbligatoria, letta da variabili d'ambiente (file .env sul server, mai nel repository).</summary>
public sealed class StartupSettings
{
    public string? ConnectionString { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string? DataProtectionCertificate { get; set; }

    public bool IsProduction { get; set; }
}

/// <summary>
/// Fa fallire l'avvio con un messaggio chiaro se mancano segreti o se contengono segnaposto:
/// meglio un container che non parte di uno che gira con credenziali deboli.
/// La password dell'amministratore è verificata con le stesse regole di Identity (unica fonte), così un
/// errore emerge all'avvio e non a migrazioni già applicate.
/// </summary>
internal sealed class StartupSettingsValidation(IOptions<IdentityOptions> identity) : IValidateOptions<StartupSettings>
{
    private static readonly string[] Placeholders = ["change-me", "changeme", "password", "example", "todo"];

    public static void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<StartupSettings>()
            .Configure(s =>
            {
                s.ConnectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName);
                s.AdminEmail = configuration["Bootstrap:AdminEmail"];
                s.AdminPassword = configuration["Bootstrap:AdminPassword"];
                s.DataProtectionCertificate = configuration[Hosting.DataProtectionCertificate.ConfigurationKey];
                s.IsProduction = environment.IsProduction();
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<StartupSettings>, StartupSettingsValidation>();
    }

    public ValidateOptionsResult Validate(string? name, StartupSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("Manca ConnectionStrings__MoscoRun (stringa di connessione PostgreSQL).");
        }
        else if (!TryGetPassword(options.ConnectionString, out var dbPassword))
        {
            failures.Add("ConnectionStrings__MoscoRun non è una stringa di connessione PostgreSQL valida.");
        }
        else if (string.IsNullOrEmpty(dbPassword) || ContainsPlaceholder(dbPassword))
        {
            failures.Add("La password del database in ConnectionStrings__MoscoRun è vuota o un segnaposto: impostare quella reale.");
        }

        if (!string.IsNullOrWhiteSpace(options.AdminEmail))
        {
            var policy = PasswordPolicyViolations(options.AdminPassword ?? string.Empty, identity.Value.Password);
            if (policy.Count > 0)
            {
                failures.Add($"Bootstrap__AdminPassword non rispetta la politica delle password: {string.Join(", ", policy)}.");
            }
            else if (ContainsPlaceholder(options.AdminPassword!))
            {
                failures.Add("Bootstrap__AdminPassword contiene un valore segnaposto.");
            }
        }

        if (options.IsProduction || !string.IsNullOrWhiteSpace(options.DataProtectionCertificate))
        {
            if (Hosting.DataProtectionCertificate.TryLoad(options.DataProtectionCertificate, out var certificate, out var error))
            {
                certificate!.Dispose();
            }
            else
            {
                failures.Add($"DataProtection__CertificateBase64 {error}: in produzione è obbligatorio (lo genera ./moscorun.sh update).");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    internal static List<string> PasswordPolicyViolations(string password, PasswordOptions rules)
    {
        var violations = new List<string>();
        if (password.Length < rules.RequiredLength)
        {
            violations.Add($"almeno {rules.RequiredLength} caratteri");
        }

        if (rules.RequireUppercase && !password.Any(char.IsAsciiLetterUpper))
        {
            violations.Add("una lettera maiuscola");
        }

        if (rules.RequireLowercase && !password.Any(char.IsAsciiLetterLower))
        {
            violations.Add("una lettera minuscola");
        }

        if (rules.RequireDigit && !password.Any(char.IsAsciiDigit))
        {
            violations.Add("una cifra");
        }

        if (rules.RequireNonAlphanumeric && password.All(char.IsAsciiLetterOrDigit))
        {
            violations.Add("un carattere non alfanumerico");
        }

        if (password.Distinct().Count() < rules.RequiredUniqueChars)
        {
            violations.Add($"almeno {rules.RequiredUniqueChars} caratteri diversi");
        }

        return violations;
    }

    private static bool TryGetPassword(string connectionString, out string? password)
    {
        try
        {
            password = new NpgsqlConnectionStringBuilder(connectionString).Password;
            return true;
        }
        catch (ArgumentException)
        {
            password = null;
            return false;
        }
    }

    private static bool ContainsPlaceholder(string value) =>
        Placeholders.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase));
}
