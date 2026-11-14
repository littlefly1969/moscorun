using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;
using Npgsql;

namespace MoscoRun.Api.Features.Workouts;

internal static class WorkoutEndpoints
{
    public const int MaxListItems = 1000;
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

        var workouts = v1.MapGroup("/workouts").RequireAuthorization(Policies.User).WithTags("Allenamenti");
        workouts.MapGet("/", ListAsync).WithName("ListWorkouts");
        workouts.MapGet("/{id:guid}", GetAsync).WithName("GetWorkout");
        workouts.MapGet("/{id:guid}/route", GetRouteAsync)
            .WithName("GetWorkoutRoute")
            .WithSummary("Tracciato come Feature GeoJSON, semplificato a maxPoints punti conservandone la forma.");
        workouts.MapGet("/{id:guid}/series", GetSeriesAsync).WithName("GetWorkoutSeries");

        return v1;
    }

    internal static async Task<Results<Created<WorkoutSummaryDto>, Ok<WorkoutSummaryDto>, ValidationProblem, ProblemHttpResult>> IngestAsync(
        IngestWorkoutRequest request,
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        TimeProvider time,
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
                    () => StoreAsync(db, content.Value, ownerId, deviceId, now, ct));
            }
            catch (DbUpdateException ex) when (attempt == 0 && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private static async Task<Results<Created<WorkoutSummaryDto>, Ok<WorkoutSummaryDto>, ValidationProblem, ProblemHttpResult>> StoreAsync(
        MoscoRunDbContext db, WorkoutContent incoming, Guid ownerId, Guid deviceId, DateTimeOffset now, CancellationToken ct)
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
            var created = WorkoutSummaryDto.From(incoming.Workout);
            return TypedResults.Created($"/api/v1/workouts/{created.Id}", created);
        }

        switch (existing.CompareWith(incoming))
        {
            case WorkoutReplay.Identical:
                // Un reinvio identico conferma che il telefono è sincronizzato.
                await MarkIngestedAsync(db, deviceId, now, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(WorkoutSummaryDto.From(existing));

            case WorkoutReplay.Newer:
                var replaced = existing.ReplaceWith(incoming, now);
                await db.WorkoutRoutes.Where(r => r.WorkoutId == existing.Id).ExecuteDeleteAsync(ct);
                await db.WorkoutSeries.Where(s => s.WorkoutId == existing.Id).ExecuteDeleteAsync(ct);
                AddChildren(db, replaced);
                await MarkIngestedAsync(db, deviceId, now, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(WorkoutSummaryDto.From(existing));

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
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        var end = (to ?? time.GetUtcNow()).ToUniversalTime();
        var start = (from ?? end - DefaultRange).ToUniversalTime();
        if (start > end)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["from"] = ["'from' deve precedere 'to'."],
            });
        }

        var ownerId = principal.OwnerId();
        var items = await db.Workouts.AsNoTracking()
            .Where(w => w.OwnerId == ownerId && w.StartedAt >= start && w.StartedAt <= end)
            .OrderByDescending(w => w.StartedAt)
            .Take(MaxListItems)
            .ToListAsync(ct);

        return TypedResults.Ok(new WorkoutList([.. items.Select(WorkoutSummaryDto.From)], start, end));
    }

    internal static async Task<Results<Ok<WorkoutDetailDto>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal principal, MoscoRunDbContext db, CancellationToken ct)
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
            WorkoutSummaryDto.From(workout),
            [.. series.Select(s => new SeriesInfoDto(s.Metric, s.Metric.Unit(), s.SampleCount))]));
    }

    internal static async Task<Results<Ok<RouteFeature>, StatusCodeHttpResult, NotFound, ValidationProblem>> GetRouteAsync(
        Guid id,
        int? maxPoints,
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var limit = maxPoints ?? DefaultRoutePoints;
        if (limit is < 2 or > MaxRoutePoints)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["maxPoints"] = [$"maxPoints deve essere compreso tra 2 e {MaxRoutePoints}."],
            });
        }

        var workout = await FindOwnedAsync(db, id, principal.OwnerId(), ct);
        if (workout is null || !workout.HasTrack)
        {
            return TypedResults.NotFound();
        }

        // Il contenuto cambia solo con una nuova revisione: l'impronta identifica anche la risposta.
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(workout.ContentHash.AsSpan(0, 12))}-{limit}\"");
        http.Response.Headers.ETag = etag.ToString();
        http.Response.Headers.CacheControl = "private, no-cache";
        if (http.Request.GetTypedHeaders().IfNoneMatch is { Count: > 0 } match && match.Any(m => m.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        var route = await db.WorkoutRoutes.AsNoTracking().SingleAsync(r => r.WorkoutId == id, ct);
        var points = route.Points();
        var simplified = RouteSimplifier.Simplify(points, limit);
        var bounds = workout.Bounds!;

        var coordinates = simplified
            .Select(p => p.AltitudeM is { } alt ? new[] { p.Longitude, p.Latitude, alt } : [p.Longitude, p.Latitude])
            .ToArray();
        var feature = new RouteFeature(
            "Feature",
            [bounds.MinLongitude, bounds.MinLatitude, bounds.MaxLongitude, bounds.MaxLatitude],
            new LineStringGeometry("LineString", coordinates),
            new RouteProperties(id, points.Count, simplified.Count, [.. simplified.Select(p => p.Time.ToUnixTimeMilliseconds())]));
        return TypedResults.Ok(feature);
    }

    internal static async Task<Results<Ok<WorkoutSeriesList>, NotFound>> GetSeriesAsync(
        Guid id, ClaimsPrincipal principal, MoscoRunDbContext db, CancellationToken ct)
    {
        var workout = await FindOwnedAsync(db, id, principal.OwnerId(), ct);
        if (workout is null)
        {
            return TypedResults.NotFound();
        }

        var series = await db.WorkoutSeries.AsNoTracking().Where(s => s.WorkoutId == id).OrderBy(s => s.Metric).ToListAsync(ct);
        return TypedResults.Ok(new WorkoutSeriesList([.. series.Select(s =>
        {
            var samples = s.Samples();
            return new SeriesDto(
                s.Metric,
                s.Metric.Unit(),
                [.. samples.Select(x => x.Time.ToUnixTimeMilliseconds())],
                [.. samples.Select(x => x.Value)]);
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
