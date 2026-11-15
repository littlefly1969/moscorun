namespace MoscoRun.Api.Auth;

public static class Policies
{
    /// <summary>Persona autenticata dal cruscotto web (cookie di sessione).</summary>
    public const string User = "user";

    /// <summary>Agente di acquisizione autenticato con token di dispositivo.</summary>
    public const string Ingest = "ingest";

    /// <summary>
    /// Lettura degli allenamenti: la persona dal cruscotto oppure il suo telefono (token di tipo PhoneBridge),
    /// che mostra i percorsi salvati sul server. Gli agenti della bilancia restano limitati all'acquisizione.
    /// </summary>
    public const string WorkoutReader = "workout-reader";
}

public static class MoscoRunClaims
{
    public const string DeviceId = "moscorun:device_id";

    public const string DeviceKind = "moscorun:device_kind";
}
