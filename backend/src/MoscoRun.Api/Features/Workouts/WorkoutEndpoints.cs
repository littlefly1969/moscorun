using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;
using Npgsql;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutEndpoints
{
    public const int MaxListItems = 1000;
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;
    public const int DefaultRoutePoints = 2000;
    public const int MaxRoutePoints = Workout.MaxTrackPoints;
    public static readonly TimeSpan DefaultRange = TimeSpan.FromDays(90);

    public static RouteGroupBuilder MapWorkoutEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/ingest/workouts", IngestAsync)
            .RequireAuthorization(Policies.Ingest)
            .WithTags("Acquisizione")
            .WithName("IngestWorkout")
            .WithSummary("Registra o aggiorna un allenamento svolto, con tracciato GPS e serie.")
            .WithDescription(
                "Idempotente su (source, sourceId) per persona. 201 alla prima ricezione; 200 se il contenuto è identico " +
                "o se revision è maggiore di quella salvata (aggiornamento); 409 se il contenuto differisce senza revisione maggiore. " +
                "Il corpo può essere compresso con Content-Encoding: gzip.");

        var workouts = v1.MapGroup("/workouts").RequireAuthorization(Policies.WorkoutReader).WithTags("Allenamenti");
        workouts.MapGet("/", ListAsync)
            .WithName("ListWorkouts")
            .WithSummary("Riepiloghi degli allenamenti, dal più recente; paginati a cursore con pageSize o cursor.")
            .WithDescription(
                "Senza pageSize né cursor: ultimi 90 giorni (o l'intervallo from/to), al più 1000 elementi, come nelle versioni precedenti. " +
                "Con pageSize (1-200) o cursor: una pagina ordinata per (startedAt, id) decrescenti su tutto lo storico o sull'intervallo from/to; " +
                "nextCursor, se presente, va passato come cursor insieme agli stessi from/to per la pagina successiva. " +
                "Lo scorrimento è stabile: gli allenamenti aggiunti nel frattempo non spostano né ripetono quelli già letti.");
        workouts.MapGet("/{id:guid}", GetAsync).WithName("GetWorkout");
        workouts.MapGet("/{id:guid}/route", GetRouteAsync)
            .WithName("GetWorkoutRoute")
            .WithSummary("Tracciato come Feature GeoJSON, semplificato a maxPoints punti conservandone la forma.");
        workouts.MapGet("/{id:guid}/series", GetSeriesAsync)
            .WithName("GetWorkoutSeries")
            .WithSummary("Serie temporali dell'allenamento; metric ne seleziona una, maxPoints la riduce per i grafici.")
            .WithDescription(
                "Senza parametri restituisce tutte le serie complete. metric limita la risposta a una grandezza (elenco vuoto se " +
                "l'allenamento non la contiene). maxPoints riduce ogni serie conservando minimi e massimi reali di ogni intervallo; " +
                "totalSamples indica quanti campioni sono salvati.");

        workouts.MapGet("/{id:guid}/science", WorkoutScienceEndpoints.GetAsync)
            .RequireAuthorization(Policies.User)
            .WithName("GetWorkoutScience");
        workouts.MapGet("/{id:guid}/science/durability", WorkoutScienceEndpoints.GetDurabilityAsync)
            .RequireAuthorization(Policies.User)
            .WithName("GetWorkoutDurability");

        return v1;
    }

    internal static async Task<Results<Created<WorkoutSummaryDto>, Ok<WorkoutSummaryDto>, ValidationProblem, ProblemHttpResult>> IngestAsync(
        IngestWorkoutRequest request,
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        TimeProvider time,
        IOptions<PrivacyOptions> privacy,
        CancellationToken ct)
    {
        var recording = request.ToRecording();
        if (!recording.IsSuccess)
        {
            return recording.Errors.ToValidationProblem();
        }

        var ownerId = principal.OwnerId();
        var deviceId = principal.DeviceId();
        var now = time.GetUtcNow();
        var content = Workout.Create(ownerId, deviceId, recording.Value, now);
        if (!content.IsSuccess)
        {
            return content.Errors.ToValidationProblem();
        }

        // Una seconda prova copre l'inserimento concorrente dello stesso allenamento: la seconda richiesta
        // trova la riga del vincitore e prosegue come reinvio.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(
                    () => StoreAsync(db, content.Value, ownerId, deviceId, now, privacy.Value.Zones.Count > 0, ct));
            }
            catch (DbUpdateException ex) when (attempt == 0 && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private static async Task<Results<Created<WorkoutSummaryDto>, Ok<WorkoutSummaryDto>, ValidationProblem, ProblemHttpResult>> StoreAsync(
        MoscoRunDbContext db, WorkoutContent incoming, Guid ownerId, Guid deviceId, DateTimeOffset now, bool hideBounds, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = incoming.Workout.Source;
        var sourceId = incoming.Workout.SourceId;

        // Blocco della riga: due revisioni concorrenti dello stesso allenamento vengono applicate in sequenza.
        var existing = await db.Workouts
            .FromSql($"SELECT * FROM moscorun.workouts WHERE owner_id = {ownerId} AND source = {source} AND source_id = {sourceId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

        if (existing is null)
        {
            Add(db, incoming);
            await MarkIngestedAsync(db, deviceId, now, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var created = WorkoutSummaryDto.From(incoming.Workout, hideBounds);
            return TypedResults.Created($"/api/v1/workouts/{created.Id}", created);
        }

        switch (existing.CompareWith(incoming))
        {
            case WorkoutReplay.Identical:
                // Un reinvio identico conferma che il telefono è sincronizzato.
                await MarkIngestedAsync(db, deviceId, now, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(WorkoutSummaryDto.From(existing, hideBounds));

            case WorkoutReplay.Newer:
                var replaced = existing.ReplaceWith(incoming, now);
                await db.WorkoutRoutes.Where(r => r.WorkoutId == existing.Id).ExecuteDeleteAsync(ct);
                await db.WorkoutSeries.Where(s => s.WorkoutId == existing.Id).ExecuteDeleteAsync(ct);
                AddChildren(db, replaced);
                await MarkIngestedAsync(db, deviceId, now, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(WorkoutSummaryDto.From(existing, hideBounds));

            default:
                return TypedResults.Problem(
                    "Lo stesso allenamento è già registrato con contenuto diverso e revisione non successiva.",
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflitto di revisione",
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["existingId"] = existing.Id,
                        ["existingRevision"] = existing.Revision,
                    });
        }
    }

    internal static async Task<Results<Ok<WorkoutList>, ValidationProblem>> ListAsync(
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        TimeProvider time,
        IOptions<PrivacyOptions> privacy,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? pageSize,
        string? cursor,
        CancellationToken ct)
    {
        var paged = pageSize is not null || cursor is not null;
        var end = (to ?? time.GetUtcNow()).ToUniversalTime();

        // La finestra predefinita di 90 giorni vale solo per la forma storica della richiesta: una lista paginata
        // deve poter scorrere tutto lo storico.
        var start = from?.ToUniversalTime() ?? (paged ? (DateTimeOffset?)null : end - DefaultRange);
        if (start > end)
        {
            return Invalid("from", "'from' deve precedere 'to'.");
        }

        var size = pageSize ?? DefaultPageSize;
        if (size is < 1 or > MaxPageSize)
        {
            return Invalid("pageSize", $"pageSize deve essere compreso tra 1 e {MaxPageSize}.");
        }

        WorkoutCursor? after = null;
        if (cursor is not null)
        {
            if (!WorkoutCursor.TryParse(cursor, out var parsed))
            {
                return Invalid("cursor", "cursor non valido: ripartire dalla prima pagina.");
            }

            after = parsed;
        }

        var ownerId = principal.OwnerId();
        var query = db.Workouts.AsNoTracking().Where(w => w.OwnerId == ownerId && w.StartedAt <= end);
        if (start is { } lower)
        {
            query = query.Where(w => w.StartedAt >= lower);
        }

        if (after is { } position)
        {
            // Paginazione a chiave: la pagina successiva riparte dopo l'ultimo elemento letto, quindi inserimenti
            // e cancellazioni concorrenti non fanno saltare né ripetere righe.
            query = query.Where(w => w.StartedAt < position.StartedAt
                || (w.StartedAt == position.StartedAt && w.Id.CompareTo(position.Id) < 0));
        }

        var take = paged ? size + 1 : MaxListItems;
        var items = await query
            .OrderByDescending(w => w.StartedAt)
            .ThenByDescending(w => w.Id)
            .Take(take)
            .ToListAsync(ct);

        string? next = null;
        if (paged && items.Count > size)
        {
            items.RemoveAt(size);
            next = new WorkoutCursor(items[^1].StartedAt, items[^1].Id).ToString();
        }

        var hide = privacy.Value.Zones.Count > 0;
        return TypedResults.Ok(new WorkoutList([.. items.Select(w => WorkoutSummaryDto.From(w, hide))], start, end, next));
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    internal static async Task<Results<Ok<WorkoutDetailDto>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal principal, MoscoRunDbContext db, IOptions<PrivacyOptions> privacy, CancellationToken ct)
    {
        var workout = await FindOwnedAsync(db, id, principal.OwnerId(), ct);
        if (workout is null)
        {
            return TypedResults.NotFound();
        }

        var series = await db.WorkoutSeries.AsNoTracking()
            .Where(s => s.WorkoutId == id)
            .OrderBy(s => s.Metric)
            .Select(s => new { s.Metric, s.SampleCount })
            .ToListAsync(ct);

        return TypedResults.Ok(new WorkoutDetailDto(
            WorkoutSummaryDto.From(workout, privacy.Value.Zones.Count > 0),
            [.. series.Select(s => new SeriesInfoDto(s.Metric, s.Metric.Unit(), s.SampleCount))]));
    }

    internal static async Task<Results<Ok<RouteFeature>, StatusCodeHttpResult, NotFound, ValidationProblem>> GetRouteAsync(
        Guid id,
        int? maxPoints,
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        HttpContext http,
        IOptions<PrivacyOptions> privacy,
        CancellationToken ct)
    {
        var limit = maxPoints ?? DefaultRoutePoints;
        if (limit is < 2 or > MaxRoutePoints)
        {
            return Invalid("maxPoints", $"maxPoints deve essere compreso tra 2 e {MaxRoutePoints}.");
        }

        var workout = await FindOwnedAsync(db, id, principal.OwnerId(), ct);
        if (workout is null || !workout.HasTrack)
        {
            return TypedResults.NotFound();
        }

        // La risposta dipende dal contenuto (impronta), dal numero di punti e dalle zone private configurate.
        var zones = privacy.Value.Zones;
        var zonesTag = zones.Count == 0 ? "0" : Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(privacy.Value.RouteZones ?? string.Empty)).AsSpan(0, 6));
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(workout.ContentHash.AsSpan(0, 12))}-{limit}-{zonesTag}\"");
        http.Response.Headers.ETag = etag.ToString();
        http.Response.Headers.CacheControl = "private, no-cache";
        if (http.Request.GetTypedHeaders().IfNoneMatch is { Count: > 0 } match && match.Any(m => m.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        var route = await db.WorkoutRoutes.AsNoTracking().SingleAsync(r => r.WorkoutId == id, ct);
        var points = route.Points();
        var visible = RoutePrivacy.Apply(points, zones);
        if (visible.Points.Count < 2)
        {
            // Percorso interamente dentro una zona privata: non c'è nulla da mostrare.
            return TypedResults.NotFound();
        }

        var simplified = RouteSimplifier.Simplify(visible.Points, limit);
        var bounds = GeoBounds.Of(visible.Points)!;

        var coordinates = simplified
            .Select(p => p.AltitudeM is { } alt ? new[] { p.Longitude, p.Latitude, alt } : [p.Longitude, p.Latitude])
            .ToArray();
        var feature = new RouteFeature(
            "Feature",
            [bounds.MinLongitude, bounds.MinLatitude, bounds.MaxLongitude, bounds.MaxLatitude],
            new LineStringGeometry("LineString", coordinates),
            new RouteProperties(id, points.Count, simplified.Count, visible.HiddenStart, visible.HiddenEnd, [.. simplified.Select(p => p.Time.ToUnixTimeMilliseconds())]));
        return TypedResults.Ok(feature);
    }

    internal static async Task<Results<Ok<WorkoutSeriesList>, NotFound, ValidationProblem>> GetSeriesAsync(
        Guid id, SeriesMetric? metric, int? maxPoints, ClaimsPrincipal principal, MoscoRunDbContext db, CancellationToken ct)
    {
        if (maxPoints is < SeriesDownsampler.MinPoints or > Workout.MaxSeriesSamples)
        {
            return Invalid("maxPoints", $"maxPoints deve essere compreso tra {SeriesDownsampler.MinPoints} e {Workout.MaxSeriesSamples}.");
        }

        if (metric is { } requested && !Enum.IsDefined(requested))
        {
            return Invalid("metric", "Grandezza sconosciuta.");
        }

        var workout = await FindOwnedAsync(db, id, principal.OwnerId(), ct);
        if (workout is null)
        {
            return TypedResults.NotFound();
        }

        var query = db.WorkoutSeries.AsNoTracking().Where(s => s.WorkoutId == id);
        if (metric is { } only)
        {
            query = query.Where(s => s.Metric == only);
        }

        var series = await query.OrderBy(s => s.Metric).ToListAsync(ct);
        return TypedResults.Ok(new WorkoutSeriesList([.. series.Select(s =>
        {
            var all = s.Samples();
            var samples = maxPoints is { } limit ? SeriesDownsampler.Downsample(all, limit) : all;
            return new SeriesDto(
                s.Metric,
                s.Metric.Unit(),
                [.. samples.Select(x => x.Time.ToUnixTimeMilliseconds())],
                [.. samples.Select(x => x.Value)],
                all.Count);
        })]));
    }

    private static Task<Workout?> FindOwnedAsync(MoscoRunDbContext db, Guid id, Guid ownerId, CancellationToken ct) =>
        db.Workouts.AsNoTracking().SingleOrDefaultAsync(w => w.Id == id && w.OwnerId == ownerId, ct);

    private static void Add(MoscoRunDbContext db, WorkoutContent content)
    {
        db.Workouts.Add(content.Workout);
        AddChildren(db, content);
    }

    private static void AddChildren(MoscoRunDbContext db, WorkoutContent content)
    {
        if (content.Route is { } route)
        {
            db.WorkoutRoutes.Add(route);
        }

        db.WorkoutSeries.AddRange(content.Series);
    }

    private static async Task MarkIngestedAsync(MoscoRunDbContext db, Guid deviceId, DateTimeOffset now, CancellationToken ct)
    {
        var device = await db.Devices.FindAsync([deviceId], ct)
            ?? throw new InvalidOperationException("Dispositivo autenticato non trovato.");
        device.RecordIngestion(now);
    }
}
