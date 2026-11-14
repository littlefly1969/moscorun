using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Measurements;
using MoscoRun.Infrastructure.Persistence;
using Npgsql;

namespace MoscoRun.Api.Features.Weighings;

internal static class WeighingEndpoints
{
    public const int MaxListItems = 1000;
    public static readonly TimeSpan DefaultRange = TimeSpan.FromDays(90);

    public static RouteGroupBuilder MapWeighingEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/ingest/weighings", IngestAsync)
            .RequireAuthorization(Policies.Ingest)
            .WithTags("Acquisizione")
            .WithName("IngestWeighing")
            .WithSummary("Registra una pesata definitiva in modo idempotente.")
            .WithDescription("201 alla prima ricezione, 200 se lo stesso evento è reinviato identico, 409 se lo stesso sourceEventId arriva con valori diversi.");

        var weighings = v1.MapGroup("/weighings").RequireAuthorization(Policies.User).WithTags("Pesate");
        weighings.MapGet("/", ListAsync).WithName("ListWeighings");
        weighings.MapPut("/{id:guid}/attribution", UpdateAttributionAsync).WithName("UpdateWeighingAttribution");

        return v1;
    }

    internal static async Task<Results<Created<WeighingDto>, Ok<WeighingDto>, ValidationProblem, ProblemHttpResult>> IngestAsync(
        IngestWeighingRequest request,
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var deviceId = principal.DeviceId();
        var reading = new WeighingReading(request.SourceEventId!, request.MeasuredAt!.Value, request.WeightKg!.Value, request.ImpedanceOhm);

        var created = Weighing.Create(principal.OwnerId(), deviceId, reading, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Errors.ToValidationProblem();
        }

        var now = time.GetUtcNow();
        var existing = await FindAsync(db, deviceId, reading.SourceEventId, ct);
        if (existing is not null)
        {
            return await ReplayAsync(db, existing, reading, now, ct);
        }

        db.Weighings.Add(created.Value);
        await MarkIngestedAsync(db, deviceId, now, ct);
        try
        {
            // Pesata e freschezza del dispositivo nella stessa transazione.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Due invii concorrenti dello stesso evento: vince il primo commit.
            db.ChangeTracker.Clear();
            var winner = await FindAsync(db, deviceId, reading.SourceEventId, ct)
                ?? throw new InvalidOperationException("Violazione di unicità senza riga esistente.", ex);
            return await ReplayAsync(db, winner, reading, now, ct);
        }

        var dto = WeighingDto.From(created.Value);
        return TypedResults.Created($"/api/v1/weighings/{dto.Id}", dto);
    }

    internal static async Task<Results<Ok<WeighingList>, ValidationProblem>> ListAsync(
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
        var items = await db.Weighings.AsNoTracking()
            .Where(w => w.OwnerId == ownerId && w.MeasuredAt >= start && w.MeasuredAt <= end)
            .OrderByDescending(w => w.MeasuredAt)
            .Take(MaxListItems)
            .ToListAsync(ct);

        return TypedResults.Ok(new WeighingList([.. items.Select(WeighingDto.From)], start, end));
    }

    internal static async Task<Results<Ok<WeighingDto>, NotFound>> UpdateAttributionAsync(
        Guid id,
        UpdateAttributionRequest request,
        ClaimsPrincipal principal,
        MoscoRunDbContext db,
        CancellationToken ct)
    {
        var ownerId = principal.OwnerId();
        var weighing = await db.Weighings.SingleOrDefaultAsync(w => w.Id == id && w.OwnerId == ownerId, ct);
        if (weighing is null)
        {
            return TypedResults.NotFound();
        }

        weighing.SetAttribution(request.Attribution!.Value);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(WeighingDto.From(weighing));
    }

    private static Task<Weighing?> FindAsync(MoscoRunDbContext db, Guid deviceId, string sourceEventId, CancellationToken ct) =>
        db.Weighings.AsNoTracking().SingleOrDefaultAsync(w => w.DeviceId == deviceId && w.SourceEventId == sourceEventId, ct);

    private static async Task MarkIngestedAsync(MoscoRunDbContext db, Guid deviceId, DateTimeOffset now, CancellationToken ct)
    {
        var device = await db.Devices.FindAsync([deviceId], ct)
            ?? throw new InvalidOperationException("Dispositivo autenticato non trovato.");
        device.RecordIngestion(now);
    }

    private static async Task<Results<Created<WeighingDto>, Ok<WeighingDto>, ValidationProblem, ProblemHttpResult>> ReplayAsync(
        MoscoRunDbContext db, Weighing existing, WeighingReading reading, DateTimeOffset now, CancellationToken ct)
    {
        if (!existing.IsSameMeasurementAs(reading))
        {
            return Conflict(existing);
        }

        // Un reinvio identico conferma che l'agente è sincronizzato: conta come acquisizione riuscita.
        await MarkIngestedAsync(db, existing.DeviceId, now, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(WeighingDto.From(existing));
    }

    private static ProblemHttpResult Conflict(Weighing existing) =>
        TypedResults.Problem(
                "Lo stesso sourceEventId è già registrato con valori diversi.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflitto di idempotenza",
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["existingId"] = existing.Id });
}
