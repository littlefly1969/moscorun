using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Api.Auth;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Measurements;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Dashboard;

/// <param name="LatestWeightKg">Ultima pesata attribuita (non esclusa).</param>
/// <param name="LatestMeasuredAt">Istante dell'ultima pesata.</param>
/// <param name="Trend7Days">Variazione della media mobile a 7 giorni rispetto a 7 giorni prima.</param>
/// <param name="Trend30Days">Variazione della media mobile a 7 giorni rispetto a 30 giorni prima.</param>
/// <param name="WeighingsLast30Days">Numero di pesate negli ultimi 30 giorni (copertura).</param>
/// <param name="Sources">Freschezza di ogni dispositivo attivo.</param>
public sealed record DashboardSummary(
    decimal? LatestWeightKg,
    DateTimeOffset? LatestMeasuredAt,
    decimal? Trend7Days,
    decimal? Trend30Days,
    int WeighingsLast30Days,
    IReadOnlyList<SourceFreshness> Sources);

public sealed record SourceFreshness(Guid DeviceId, string Name, DateTimeOffset? LastSeenAt);

internal static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/dashboard/summary", GetSummaryAsync)
            .RequireAuthorization(Policies.User)
            .WithTags("Cruscotto")
            .WithName("GetDashboardSummary");
        return v1;
    }

    internal static async Task<Ok<DashboardSummary>> GetSummaryAsync(
        ClaimsPrincipal principal, MoscoRunDbContext db, TimeProvider time, CancellationToken ct)
    {
        var ownerId = principal.OwnerId();
        var now = time.GetUtcNow();
        var windowStart = now - TimeSpan.FromDays(30 + WeightTrend.SmoothingDays);

        var points = await db.Weighings.AsNoTracking()
            .Where(w => w.OwnerId == ownerId && w.Attribution != AttributionStatus.Rejected && w.MeasuredAt >= windowStart)
            .OrderBy(w => w.MeasuredAt)
            .Select(w => new WeightPoint(w.MeasuredAt, w.WeightKg))
            .ToListAsync(ct);

        var sources = await db.Devices.AsNoTracking()
            .Where(d => d.OwnerId == ownerId && d.RevokedAt == null)
            .OrderBy(d => d.Name)
            .Select(d => new SourceFreshness(d.Id, d.Name, d.LastSeenAt))
            .ToListAsync(ct);

        var latest = points.Count > 0 ? points[^1] : null;
        return TypedResults.Ok(new DashboardSummary(
            latest?.WeightKg,
            latest?.MeasuredAt,
            WeightTrend.Change(points, now, TimeSpan.FromDays(7)),
            WeightTrend.Change(points, now, TimeSpan.FromDays(30)),
            points.Count(p => p.MeasuredAt >= now - TimeSpan.FromDays(30)),
            sources));
    }
}
