using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Science;

/// <summary>
/// Acquisizione dei dati scientifici dei wearable: osservazioni puntuali (C2/C3) e chunk di segnale lossless.
/// Atleta e dispositivo vengono dalle credenziali; gli allenamenti sono indicati per identità naturale.
/// </summary>
internal static class ScientificIngestEndpoints
{
    private const int MaxText = 200;
    private const int MaxExternalId = 512;
    private const int MaxCursor = 2048;

    public static RouteGroupBuilder MapScientificIngestEndpoints(this RouteGroupBuilder v1)
    {
        var ingest = v1.MapGroup("/ingest").RequireAuthorization(Policies.ScientificIngest).WithTags("Acquisizione");
        ingest.MapPost("/observations", IngestObservationsAsync)
            .WithName("IngestObservations")
            .WithSummary("Registra fino a 500 osservazioni dei wearable (HRV, sonno, frequenza a riposo, VO2max...) in modo atomico.")
            .WithDescription(
                "Idempotente su (source, externalId, revision, metric) per persona: un reinvio identico non scrive nulla, " +
                "la stessa revisione con contenuto diverso è un conflitto (409). Con page il checkpoint (source, stream) " +
                "avanza nella stessa transazione; previousCursor diverso da quello salvato è un conflitto (409). " +
                "Solo token di tipo telefono. Il corpo può essere compresso con Content-Encoding: gzip.");
        ingest.MapGet("/observations/cursor", ReadCursorAsync)
            .WithName("GetObservationCursor")
            .WithSummary("Checkpoint salvato per (source, stream); null se mai scritto.");
        ingest.MapPost("/signals", IngestSignalAsync)
            .WithName("IngestSignal")
            .WithSummary("Registra un chunk di segnale ad alta frequenza (RR, dinamiche di corsa, meteo...), senza perdita.")
            .WithDescription(
                "Al più 16384 campioni in ordine di tempo strettamente crescente. 201 al primo arrivo, 200 se lo stesso " +
                "chunk era già presente con contenuto identico, 409 se la stessa revisione arriva con contenuto diverso.");
        return v1;
    }

    internal static async Task<Results<Ok<IngestObservationsResult>, ValidationProblem, ProblemHttpResult>> IngestObservationsAsync(
        IngestObservationsRequest request, ClaimsPrincipal principal, MoscoRunDbContext db, WearableImport import,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var errors = new Errors();
        var now = time.GetUtcNow();
        ValidateSource(errors, request.Source);
        var samples = request.Samples;
        if (samples is null || samples.Count > ScientificImportStore.MaxBatchSize || (samples.Count == 0 && request.Page is null))
        {
            errors.Add("samples", $"Da 1 a {ScientificImportStore.MaxBatchSize} campioni (zero solo con page).");
        }

        var page = request.Page;
        if (page is not null)
        {
            ValidateText(errors, "page.stream", page.Stream, MaxText);
            ValidateText(errors, "page.nextCursor", page.NextCursor, MaxCursor);
            if (page.PreviousCursor is { Length: 0 or > MaxCursor })
            {
                errors.Add("page.previousCursor", $"Null oppure da 1 a {MaxCursor} caratteri.");
            }
        }

        for (var i = 0; samples is not null && i < samples.Count && errors.Count < 50; i++)
        {
            ValidateObservation(errors, $"samples[{i}]", samples[i], now);
        }

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        var owner = principal.OwnerId();
        var workouts = await ResolveWorkoutsAsync(db, owner, samples!.Select(s => s.Workout), errors, "samples", ct);
        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        var deviceId = principal.DeviceId();
        var items = samples!.Select(s => new WearableSample(s.ExternalId!, s.Revision!, s.Metric!, s.Unit!, s.Value,
            s.Time, s.Method, s.ProviderAlgorithm, s.RevisionOrder, new ScientificImportProvenance(deviceId,
                s.Workout is null ? null : workouts[Key(s.Workout)], s.Native?.Metric, s.Native?.Unit, s.Native?.Value,
                s.Native?.MappingVersion))).ToArray();
        try
        {
            var imported = page is null
                ? await import.ImportAsync(owner, request.Source!, items, ct)
                : await import.ImportPageAsync(owner, request.Source!, page.Stream!, page.PreviousCursor, page.NextCursor!, items, ct);
            return TypedResults.Ok(new IngestObservationsResult(items.Length, imported, page?.NextCursor));
        }
        catch (ArgumentException ex)
        {
            return Errors.Single("samples", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    internal static async Task<Results<Ok<SyncCursorDto>, ValidationProblem>> ReadCursorAsync(
        string? source, string? stream, ClaimsPrincipal principal, WearableImport import, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var errors = new Errors();
        ValidateSource(errors, source);
        ValidateText(errors, "stream", stream, MaxText);
        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        var cursor = await import.ReadCursorAsync(principal.OwnerId(), source!, stream!, ct);
        return TypedResults.Ok(new SyncCursorDto(source!, stream!, cursor));
    }

    internal static async Task<Results<Created<IngestSignalResult>, Ok<IngestSignalResult>, ValidationProblem, ProblemHttpResult>> IngestSignalAsync(
        IngestSignalRequest request, ClaimsPrincipal principal, MoscoRunDbContext db, ScientificSignalStore store,
        TimeProvider time, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var errors = new Errors();
        ValidateSource(errors, request.Source);
        ValidateIdentity(errors, string.Empty, request.ExternalId, request.Revision, request.RevisionOrder);
        if (!WearableIngestCatalog.AcceptsSignal(request.Metric, request.Unit))
        {
            errors.Add("metric", "Metrica non ammessa come segnale o unità diversa da quella canonica del catalogo.");
        }

        ValidateMethod(errors, string.Empty, request.Method, request.ProviderAlgorithm);
        ValidateSignalSamples(errors, request.Samples, time.GetUtcNow());
        ValidateReference(errors, "workout", request.Workout);
        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        var owner = principal.OwnerId();
        var workouts = await ResolveWorkoutsAsync(db, owner, [request.Workout], errors, string.Empty, ct);
        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        var samples = request.Samples!.Select(s => new ScientificSignalSample(s.Time, s.Value)).ToArray();
        var chunk = new ScientificSignalChunk(request.Source!, request.ExternalId!, request.Revision!, request.Metric!,
            request.Unit!, request.Method, samples, request.ProviderAlgorithm, request.RevisionOrder, principal.DeviceId(),
            request.Workout is null ? null : workouts[Key(request.Workout)]);
        var id = ScientificImportStore.Identity(new ScientificImportItem(chunk.Source, chunk.ExternalId, chunk.Revision,
            chunk.Metric, chunk.Unit, null, samples[0].Time, chunk.Method));
        try
        {
            var created = await store.ImportAsync(owner, chunk, ct);
            var result = new IngestSignalResult(id, samples.Length, created);
            return created
                ? TypedResults.Created((string?)null, result)
                : TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Errors.Single("samples", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static void ValidateObservation(Errors errors, string at, ObservationSampleDto? sample, DateTimeOffset now)
    {
        if (sample is null)
        {
            errors.Add(at, "Campione mancante.");
            return;
        }

        ValidateIdentity(errors, at + ".", sample.ExternalId, sample.Revision, sample.RevisionOrder);
        if (!WearableIngestCatalog.AcceptsObservation(sample.Metric, sample.Unit))
        {
            errors.Add(at + ".metric", "Metrica non ammessa dai wearable o unità diversa da quella canonica del catalogo.");
        }

        if (sample.Value is { } value && !double.IsFinite(value))
        {
            errors.Add(at + ".value", "Valore non finito: usare null per un valore mancante.");
        }

        if (sample.Time == default || sample.Time > now)
        {
            errors.Add(at + ".time", "Istante obbligatorio e non nel futuro rispetto al server.");
        }

        ValidateMethod(errors, at + ".", sample.Method, sample.ProviderAlgorithm);
        ValidateReference(errors, at + ".workout", sample.Workout);
        if (sample.Native is { } native && (native.Metric?.Length > MaxText || native.Unit?.Length > MaxText ||
                native.MappingVersion?.Length > MaxText || native.Value is { } nativeValue && !double.IsFinite(nativeValue)))
        {
            errors.Add(at + ".native", $"Testi al più di {MaxText} caratteri e valore finito.");
        }
    }

    private static void ValidateSignalSamples(Errors errors, IReadOnlyList<SignalSampleDto>? samples, DateTimeOffset now)
    {
        if (samples is null || samples.Count is < 1 or > ScientificSignalStore.MaxChunkSamples)
        {
            errors.Add("samples", $"Da 1 a {ScientificSignalStore.MaxChunkSamples} campioni.");
            return;
        }

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            if (sample is null || sample.Time == default || sample.Value is { } value && !double.IsFinite(value) ||
                i > 0 && samples[i - 1] is { } previous && sample.Time.UtcTicks <= previous.Time.UtcTicks)
            {
                errors.Add($"samples[{i}]", "Istanti strettamente crescenti e valori finiti (null se mancante).");
                return;
            }
        }

        if (samples[^1].Time > now)
        {
            errors.Add("samples", "L'ultimo campione è nel futuro rispetto al server.");
        }
    }

    private static void ValidateSource(Errors errors, string? source)
    {
        if (!WearableIngestCatalog.IsSource(source))
        {
            errors.Add("source", "Sorgente non supportata: " + string.Join(", ", WearableIngestCatalog.SupportedSources) + ".");
        }
    }

    private static void ValidateIdentity(Errors errors, string prefix, string? externalId, string? revision, long? order)
    {
        ValidateText(errors, prefix + "externalId", externalId, MaxExternalId);
        ValidateText(errors, prefix + "revision", revision, MaxText);
        if (order < 0)
        {
            errors.Add(prefix + "revisionOrder", "Ordine di revisione non negativo.");
        }
    }

    private static void ValidateMethod(Errors errors, string prefix, ObservationMethod method, string? algorithm)
    {
        if (!Enum.IsDefined(method) || !WearableIngestCatalog.AcceptsMethod(method))
        {
            errors.Add(prefix + "method", "Metodo ammesso: Measured, ProviderReported o ProviderEstimated.");
        }

        if (algorithm is { Length: 0 or > MaxText } || method == ObservationMethod.ProviderEstimated && string.IsNullOrWhiteSpace(algorithm))
        {
            errors.Add(prefix + "providerAlgorithm", $"Da 1 a {MaxText} caratteri; obbligatorio per ProviderEstimated.");
        }
    }

    private static void ValidateReference(Errors errors, string at, WorkoutReferenceDto? reference)
    {
        if (reference is not null && (string.IsNullOrEmpty(reference.Source) || reference.Source.Length > 32 ||
                string.IsNullOrEmpty(reference.SourceId) || reference.SourceId.Length > 128))
        {
            errors.Add(at, "Allenamento indicato con source e sourceId come all'invio dell'allenamento.");
        }
    }

    private static void ValidateText(Errors errors, string field, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
        {
            errors.Add(field, $"Obbligatorio, da 1 a {max} caratteri.");
        }
    }

    /// <summary>Risolve i riferimenti agli allenamenti dell'atleta; uno sconosciuto è un errore di validazione.</summary>
    private static async Task<Dictionary<string, Guid>> ResolveWorkoutsAsync(MoscoRunDbContext db, Guid owner,
        IEnumerable<WorkoutReferenceDto?> references, Errors errors, string field, CancellationToken ct)
    {
        var wanted = references.OfType<WorkoutReferenceDto>().DistinctBy(Key, StringComparer.Ordinal).ToArray();
        var resolved = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var bySource in wanted.GroupBy(r => r.Source!, StringComparer.Ordinal))
        {
            var ids = bySource.Select(r => r.SourceId!).ToArray();
            var rows = await db.Workouts.AsNoTracking()
                .Where(w => w.OwnerId == owner && w.Source == bySource.Key && ids.Contains(w.SourceId))
                .Select(w => new { w.SourceId, w.Id }).ToListAsync(ct);
            foreach (var row in rows)
            {
                resolved[Key(bySource.Key, row.SourceId)] = row.Id;
            }
        }

        var missing = wanted.Where(r => !resolved.ContainsKey(Key(r))).Select(Key).ToArray();
        if (missing.Length > 0)
        {
            errors.Add(string.IsNullOrEmpty(field) ? "workout" : field,
                "Allenamenti non presenti sul server: inviarli prima dei dati collegati (" + string.Join(", ", missing.Take(5)) + ").");
        }

        return resolved;
    }

    private static string Key(WorkoutReferenceDto reference) => Key(reference.Source!, reference.SourceId!);

    private static string Key(string source, string sourceId) => source + "/" + sourceId;

    private static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Conflitto di revisione o di checkpoint");

    private sealed class Errors
    {
        private readonly Dictionary<string, List<string>> _items = new(StringComparer.Ordinal);

        public int Count => _items.Count;

        public void Add(string field, string message)
        {
            if (!_items.TryGetValue(field, out var list))
            {
                _items[field] = list = [];
            }

            list.Add(message);
        }

        public ValidationProblem ToProblem() => TypedResults.ValidationProblem(
            _items.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal));

        public static ValidationProblem Single(string field, string message) => TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });
    }
}
