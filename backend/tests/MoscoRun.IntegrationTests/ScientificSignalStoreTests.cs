using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificSignalStoreTests(ApiFactory factory)
{
    [Fact]
    public async Task Chunk_roundtrip_is_lossless_idempotent_and_owner_scoped()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddHours(-1);
        var samples = new ScientificSignalSample[] { new(time, 0.123456789), new(time.AddTicks(1), null), new(time.AddTicks(2), -2.0) };
        var chunk = new ScientificSignalChunk("garmin-connect", "chunk-1", "1", "ecg", "mV", ObservationMethod.Measured, samples);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificSignalStore>();
        (await store.ImportAsync(owner, chunk, ct)).Should().BeTrue();
        (await store.ImportAsync(owner, chunk, ct)).Should().BeFalse();
        Func<Task> conflict = () => store.ImportAsync(owner, chunk with { Samples = [new(time, 2)] }, ct);
        await conflict.Should().ThrowAsync<InvalidOperationException>();
        var page = await store.ReadWindowAsync(owner, chunk.Source, chunk.Metric, time, time.AddSeconds(1), factory.Time.GetUtcNow(), ct: ct);
        page.Items.Should().ContainSingle().Which.Chunk.Samples.Should().Equal(samples);
        (await store.ReadWindowAsync(other, chunk.Source, chunk.Metric, time, time.AddSeconds(1), factory.Time.GetUtcNow(), ct: ct)).Items.Should().BeEmpty();
        (await store.ReadWindowAsync(owner, chunk.Source, chunk.Metric, time, time.AddSeconds(1), time.AddSeconds(1), ct: ct)).Items.Should().BeEmpty();
    }

    [Fact]
    public void Codec_rejects_non_finite_unordered_and_tampered_data()
    {
        var time = DateTimeOffset.UnixEpoch;
        Action nonFinite = () => ScientificSignalCodec.Encode([new(time, double.NaN)]);
        nonFinite.Should().Throw<ArgumentException>();
        Action unordered = () => ScientificSignalCodec.Encode([new(time, 1), new(time, 2)]);
        unordered.Should().Throw<ArgumentException>();
        var encoded = ScientificSignalCodec.Encode([new(time, 1)]);
        encoded.Hash[0] ^= 1;
        Action tampered = () => ScientificSignalCodec.Decode(encoded.Data, encoded.Hash, 1, ScientificSignalCodec.Version);
        tampered.Should().Throw<InvalidDataException>();
    }
}
