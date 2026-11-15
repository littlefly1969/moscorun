using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class WorkoutIngestionTests(ApiFactory factory)
{
    private const string Ingest = "/api/v1/ingest/workouts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTimeOffset Start => factory.Time.GetUtcNow().AddHours(-3);

    internal static object Payload(DateTimeOffset start, string sourceId = "uid-1:0", long revision = 1, int points = 600, double distance = 1500, double hr = 140) => new
    {
        source = "samsung-health",
        sourceId,
        revision,
        activityType = "RUNNING",
        startedAt = start,
        endedAt = start.AddSeconds(Math.Max(points, 60)),
        utcOffsetMinutes = 60,
        activeDurationSeconds = Math.Max(points, 60) - 5.5,
        distanceM = distance,
        energyKcal = 120.5,
        averageHeartRate = hr,
        maxHeartRate = 171,
        track = Enumerable.Range(0, points).Select(i => new
        {
            time = start.AddSeconds(i),
            lat = 41.89 + (i * 2e-5),
            lon = 12.49 + (Math.Sin(i / 50.0) * 1e-3),
            altitudeM = i % 5 == 0 ? (double?)null : 20 + (i * 0.01),
            accuracyM = 4.2,
        }),
        series = new object[]
        {
            new { metric = "HeartRate", samples = Enumerable.Range(0, points).Select(i => new { time = start.AddSeconds(i), value = hr + (i % 10) }) },
            new { metric = "Speed", samples = Enumerable.Range(0, points).Select(i => new { time = start.AddSeconds(i), value = 2.8 }) },
        },
    };

    private async Task<(HttpClient User, HttpClient Phone, Guid DeviceId)> SetupAsync()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, device) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        return (user, phone, device.Device.Id);
    }

    [Fact]
    public async Task First_delivery_creates_and_identical_replay_returns_same_workout()
    {
        var (_, phone, deviceId) = await SetupAsync();
        var payload = Payload(Start);

        var first = await phone.PostAsJsonAsync(Ingest, payload, Ct);
        var replay = await phone.PostAsJsonAsync(Ingest, payload, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = (await first.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!;
        var replayed = (await replay.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!;
        replayed.Should().BeEquivalentTo(created);
        created.TrackPointCount.Should().Be(600);
        created.Bounds.Should().NotBeNull();
        created.ActiveDurationSeconds.Should().Be(594.5);
        first.Headers.Location!.ToString().Should().Be($"/api/v1/workouts/{created.Id}");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        (await db.Devices.SingleAsync(d => d.Id == deviceId, Ct)).LastIngestedAt.Should().NotBeNull();
        (await db.WorkoutSeries.CountAsync(s => s.WorkoutId == created.Id, Ct)).Should().Be(2);
    }

    [Fact]
    public async Task Higher_revision_replaces_content_and_children()
    {
        var (user, phone, _) = await SetupAsync();
        var created = await (await phone.PostAsJsonAsync(Ingest, Payload(Start, points: 600), Ct))
            .Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct);

        var update = await phone.PostAsJsonAsync(Ingest, Payload(Start, revision: 2, points: 300, distance: 900), Ct);

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!;
        updated.Id.Should().Be(created!.Id);
        updated.Revision.Should().Be(2);
        updated.TrackPointCount.Should().Be(300);
        updated.DistanceM.Should().Be(900);

        var route = await user.GetFromJsonAsync<RouteFeature>($"/api/v1/workouts/{created.Id}/route?maxPoints=100000", Json.Options, Ct);
        route!.Properties.TotalPoints.Should().Be(300);
        var series = await user.GetFromJsonAsync<WorkoutSeriesList>($"/api/v1/workouts/{created.Id}/series", Json.Options, Ct);
        series!.Items.Should().OnlyContain(s => s.Times.Count == 300);
    }

    [Fact]
    public async Task Different_content_without_higher_revision_is_a_conflict()
    {
        var (_, phone, _) = await SetupAsync();
        await phone.PostAsJsonAsync(Ingest, Payload(Start, revision: 5), Ct);

        var same = await phone.PostAsJsonAsync(Ingest, Payload(Start, revision: 5, distance: 2000), Ct);
        var older = await phone.PostAsJsonAsync(Ingest, Payload(Start, revision: 4, distance: 2000), Ct);

        same.StatusCode.Should().Be(HttpStatusCode.Conflict);
        older.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await same.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem!.Extensions.Should().ContainKeys("existingId", "existingRevision");
    }

    [Fact]
    public async Task Same_workout_from_two_phones_of_the_same_person_is_stored_once()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (a, _) = await factory.CreateDeviceClientAsync(user, "A", DeviceKind.PhoneBridge);
        var (b, _) = await factory.CreateDeviceClientAsync(user, "B", DeviceKind.PhoneBridge);

        (await a.PostAsJsonAsync(Ingest, Payload(Start), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await b.PostAsJsonAsync(Ingest, Payload(Start), Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Same_source_id_for_two_people_creates_two_workouts()
    {
        var (_, first, _) = await SetupAsync();
        var (_, second, _) = await SetupAsync();

        (await first.PostAsJsonAsync(Ingest, Payload(Start), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await second.PostAsJsonAsync(Ingest, Payload(Start), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Concurrent_deliveries_store_exactly_one_workout()
    {
        var (user, phone, _) = await SetupAsync();
        var payload = Payload(Start, sourceId: "race:0", points: 120);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => phone.PostAsJsonAsync(Ingest, payload, Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(7);
        var list = await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct);
        list!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Concurrent_revisions_leave_the_highest_one()
    {
        var (user, phone, _) = await SetupAsync();
        await phone.PostAsJsonAsync(Ingest, Payload(Start, points: 60), Ct);

        var responses = await Task.WhenAll(Enumerable.Range(2, 6)
            .Select(r => phone.PostAsJsonAsync(Ingest, Payload(Start, revision: r, points: 60, distance: r * 100), Ct)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        var list = await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct);
        list!.Items.Should().ContainSingle().Which.Revision.Should().Be(7);
    }

    [Fact]
    public async Task Gzip_compressed_body_is_accepted()
    {
        var (_, phone, _) = await SetupAsync();
        using var buffer = new MemoryStream();
        await using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            await JsonSerializer.SerializeAsync(gzip, Payload(Start, points: 3600), Json.Options, Ct);
        }

        using var content = new ByteArrayContent(buffer.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");

        var response = await phone.PostAsync(Ingest, content, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!.TrackPointCount.Should().Be(3600);
    }

    [Fact]
    public async Task Invalid_workout_returns_field_errors_and_codes()
    {
        var (_, phone, _) = await SetupAsync();
        var payload = new
        {
            source = "samsung-health",
            sourceId = "bad id",
            revision = 1,
            activityType = "RUNNING",
            startedAt = Start,
            endedAt = Start.AddHours(1),
            track = new[] { new { time = Start, lat = 95.0, lon = 12.0 } },
        };

        var response = await phone.PostAsJsonAsync(Ingest, payload, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        problem!.Errors.Keys.Should().BeEquivalentTo(["sourceId", "track"]);
    }

    [Fact]
    public async Task Incomplete_nested_items_are_validation_errors_not_server_errors()
    {
        var (_, phone, _) = await SetupAsync();
        var payload = new
        {
            source = "samsung-health",
            sourceId = "x",
            revision = 1,
            activityType = "RUNNING",
            startedAt = Start,
            endedAt = Start.AddHours(1),
            track = new object?[] { null, new { time = Start } },
            series = new object?[] { new { metric = "HeartRate", samples = new object?[] { new { time = Start } } } },
        };

        var response = await phone.PostAsJsonAsync(Ingest, payload, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        problem!.Errors.Keys.Should().BeEquivalentTo(["track", "series.HeartRate"]);
    }

    [Fact]
    public async Task Missing_required_fields_are_rejected()
    {
        var (_, phone, _) = await SetupAsync();

        var response = await phone.PostAsJsonAsync(Ingest, new { source = "samsung-health" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ingestion_requires_a_device_token()
    {
        var (user, _, _) = await SetupAsync();

        (await user.PostAsJsonAsync(Ingest, Payload(Start), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClient().PostAsJsonAsync(Ingest, Payload(Start), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
