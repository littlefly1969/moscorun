using System.Data;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class RrChunkReader(MoscoRunDbContext db)
{
    private const int MaxChunks = 64;

    public async Task<RrChunkReport?> ReadAsync(Guid owner, string source, DateOnly day, DateTimeOffset asOf,
        Guid? chunkId, string context, bool screened, bool timestampsAreBeatEnds, CancellationToken ct = default)
    {
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(source) || source.Length > 200 ||
            day >= DateOnly.MaxValue || day > DateOnly.FromDateTime(asOf.UtcDateTime) ||
            chunkId == Guid.Empty || !RrChunkEngine.ValidContext(context))
        {
            return null;
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var result = await ReadCoreAsync(owner, source, day, asOf, chunkId, context, screened,
                timestampsAreBeatEnds, token);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }

    private async Task<RrChunkReport> ReadCoreAsync(Guid owner, string source, DateOnly day, DateTimeOffset asOf,
        Guid? chunkId, string context, bool screened, bool timestampsAreBeatEnds, CancellationToken ct)
    {
        var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var first = start.UtcTicks;
        var last = start.AddDays(1).UtcTicks;
        var cutoff = asOf.UtcTicks;
        var query = db.Set<ScientificSignalChunkRow>().AsNoTracking().Where(row => row.OwnerId == owner &&
            row.Source == source && row.Metric == "rrInterval" && row.FirstTicks >= first && row.FirstTicks < last &&
            row.AvailableTicks <= cutoff && row.LastTicks <= cutoff);
        var metadata = await query.OrderBy(row => row.FirstTicks).ThenBy(row => row.Id).Take(MaxChunks + 1)
            .Select(row => new RrChunkMetadata(row.Id, row.ExternalId, row.Revision, row.RevisionOrder,
                row.Unit, row.Method, row.ProviderAlgorithm, row.WorkoutId,
                new DateTimeOffset(row.FirstTicks, TimeSpan.Zero), new DateTimeOffset(row.LastTicks, TimeSpan.Zero),
                new DateTimeOffset(row.AvailableTicks, TimeSpan.Zero), row.SampleCount, Convert.ToHexStringLower(row.ContentHash)))
            .ToListAsync(ct);
        var selected = chunkId is { } id ? metadata.Take(MaxChunks).SingleOrDefault(row => row.Id == id) : null;
        RrChunkAnalysis? analysis = null;
        if (selected is not null)
        {
            var row = await query.SingleAsync(row => row.Id == selected.Id, ct);
            var samples = ScientificSignalCodec.Decode(row.Data, row.ContentHash, row.SampleCount, row.CodecVersion)
                .Select(item => new SignalSample(item.Time, item.Value)).ToArray();
            analysis = RrChunkEngine.Analyze(samples, context, screened, timestampsAreBeatEnds,
                Enum.Parse<ObservationMethod>(row.Method));
        }

        return new RrChunkReport(source, day, asOf, metadata.Count > MaxChunks,
            Array.AsReadOnly(metadata.Take(MaxChunks).ToArray()), selected, analysis);
    }
}
