namespace MoscoRun.Api.Auth;

/// <summary>
/// Registro di sicurezza degli accessi. L'indirizzo è quello del client reale, ricavato da X-Forwarded-For solo
/// quando la richiesta arriva da un proxy fidato: è lo stesso valore usato dal limite dei tentativi.
/// </summary>
internal static partial class AuthLog
{
    public const string Category = "MoscoRun.Security";

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Accesso riuscito da {ClientIp}")]
    public static partial void LoginSucceeded(ILogger logger, string clientIp);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Accesso non riuscito da {ClientIp}")]
    public static partial void LoginFailed(ILogger logger, string clientIp);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Account bloccato per troppi tentativi; ultimo da {ClientIp}")]
    public static partial void LockedOut(ILogger logger, string clientIp);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Limite dei tentativi di accesso superato da {ClientIp}")]
    public static partial void RateLimited(ILogger logger, string clientIp);
}
