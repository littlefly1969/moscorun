using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class NativeContextEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Biomechanics_resolves_explicit_chunks_and_rejects_mixed_devices()
    {
        var (client, owner) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var (device, registration) = await factory.CreateDeviceClientAsync(user, kind: DeviceKind.PhoneBridge);
        using var phone = device;
        var (second, secondRegistration) = await factory.CreateDeviceClientAsync(user, "Secondo dispositivo", DeviceKind.PhoneBridge);
        using var secondPhone = second;
        var ct = TestContext.Current.CancellationToken;
        var start = factory.Time.GetUtcNow().AddHours(-1);
        var workout = await IngestAsync(phone, start, ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificSignalStore>();
        await ImportAsync(store, owner, workout, start, "cadence", "1/min", 150, registration.Device.Id, ct);
        await ImportAsync(store, owner, workout, start, "groundContactTime", "ms", 240, registration.Device.Id, ct);
        await ImportAsync(store, owner, workout, start, "speed", "m/s", 3, secondRegistration.Device.Id, ct);
        var uri = $"/api/v1/workouts/{workout}/science/biomechanics?source=native-test&protocolConfirmed=true&cadenceIsStepsPerMinute=true&balanceIsLeftPercent=false";
        var listed = (await user.GetFromJsonAsync<ExtendedBiomechanicsReport>(uri, ct))!;
        listed.Chunks.Should().HaveCount(3);
        listed.Status.Should().Be("select-chunks");
        var cadence = Chunk(listed.Chunks, "cadence");
        var contact = Chunk(listed.Chunks, "groundContactTime");
        var speed = Chunk(listed.Chunks, "speed");
        var selected = (await user.GetFromJsonAsync<ExtendedBiomechanicsReport>(uri + $"&chunkIds={cadence},{contact}", ct))!;
        selected.Status.Should().Be("observed");
        selected.Metrics.Single(metric => string.Equals(metric.Metric, "dutyFactor", StringComparison.Ordinal))
            .WeightedMean.Should().Be(30);
        var mixed = (await user.GetFromJsonAsync<ExtendedBiomechanicsReport>(uri + $"&chunkIds={cadence},{speed}", ct))!;
        mixed.Status.Should().Be("incompatible-chunks");
        mixed.Metrics.Should().BeEmpty();
        (await stranger.GetAsync(uri + $"&chunkIds={cadence},{contact}", ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Environment_reports_only_selected_weather_and_no_route_coordinates()
    {
        var (client, owner) = await factory.CreateUserClientAsync();
        using var user = client;
        var (other, _) = await factory.CreateUserClientAsync();
        using var stranger = other;
        var (device, _) = await factory.CreateDeviceClientAsync(user, kind: DeviceKind.PhoneBridge);
        using var phone = device;
        var ct = TestContext.Current.CancellationToken;
        var start = factory.Time.GetUtcNow().AddHours(-1);
        var workout = await IngestAsync(phone, start, ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ScientificSignalStore>();
        await ImportAsync(store, owner, workout, start, "airTemperature", "Cel", 30, null, ct);
        await ImportAsync(store, owner, workout, start, "humidity", "%", 50, null, ct);
        var uri = $"/api/v1/workouts/{workout}/science/environment";
        var request = new WorkoutEnvironmentRequest("native-test", [], new WorkoutEnvironmentProtocol(25, 60, 30, 10, 5, true, true));
        var listed = await EnvironmentAsync(user, uri, request, ct);
        listed.Chunks.Should().HaveCount(2);
        listed.Analysis.HeatExposure.Should().BeNull();
        listed.Analysis.Terrain.RawAscentMeters.Should().Be(3);
        var chosen = request with { ChunkIds = listed.Chunks.Select(item => item.Id).ToArray() };
        var report = await EnvironmentAsync(user, uri, chosen, ct);
        report.WeatherStatus.Should().Be("observed-support");
        report.Analysis.HeatExposure!.DegreeMinutes.Should().BeApproximately(5.0 / 6, 1e-9);
        report.Analysis.Metrics.Single(metric => string.Equals(metric.Metric, "vaporPressureDeficit", StringComparison.Ordinal))
            .SupportedSeconds.Should().Be(10);
        using var unauthorized = await stranger.PostAsJsonAsync(uri, chosen, ct);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> IngestAsync(HttpClient phone, DateTimeOffset start, CancellationToken ct)
    {
        using var response = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId = "native-context-test",
            revision = 1,
            activityType = "RUNNING",
            startedAt = start,
            endedAt = start.AddSeconds(10),
            activeDurationSeconds = 10,
            distanceM = 33,
            track = new[]
            {
                new { time = start, lat = 0.0, lon = 0.0, altitudeM = 0, accuracyM = 1 },
                new { time = start.AddSeconds(10), lat = 0.0003, lon = 0.0, altitudeM = 3, accuracyM = 1 },
            },
        }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(ct))!.Id;
    }

    private static async Task ImportAsync(ScientificSignalStore store, Guid owner, Guid workout, DateTimeOffset start,
        string metric, string unit, double value, Guid? device, CancellationToken ct)
    {
        var chunk = new ScientificSignalChunk("native-test", metric, "r1", metric, unit, ObservationMethod.Measured,
            [new(start, value), new(start.AddSeconds(5), value), new(start.AddSeconds(10), value)],
            RevisionOrder: 1, DeviceId: device, WorkoutId: workout);
        (await store.ImportAsync(owner, chunk, ct)).Should().BeTrue();
    }

    private static Guid Chunk(IReadOnlyList<BiomechanicsChunkMetadata> chunks, string metric) =>
        chunks.Single(item => string.Equals(item.Metric, metric, StringComparison.Ordinal)).Id;

    private static async Task<WorkoutEnvironmentReport> EnvironmentAsync(HttpClient user, string uri,
        WorkoutEnvironmentRequest request, CancellationToken ct)
    {
        using var response = await user.PostAsJsonAsync(uri, request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkoutEnvironmentReport>(ct))!;
    }
}
