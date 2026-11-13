namespace MoscoRun.Api.Auth;

public static class Policies
{
    /// <summary>Persona autenticata dal cruscotto web (cookie di sessione).</summary>
    public const string User = "user";

    /// <summary>Agente di acquisizione autenticato con token di dispositivo.</summary>
    public const string Ingest = "ingest";
}

public static class MoscoRunClaims
{
    public const string DeviceId = "moscorun:device_id";
}
