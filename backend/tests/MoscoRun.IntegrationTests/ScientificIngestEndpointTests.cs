using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Api.Features.Science;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScientificIngestEndpointTests(ApiFactory factory)
{
    private const string Observations = "/api/v1/ingest/observations";
    private const string Signals = "/api/v1/ingest/signals";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTimeOffset Yesterday => new DateTimeOffset(factory.Time.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero).AddDays(-1).AddHours(5);

    private async Task<(HttpClient User, HttpClient Phone, Guid Owner, Guid Device)> PhoneAsync()
    {
        var (user, owner) = await factory.CreateUserClientAsync();
        var (phone, device) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        return (user, phone, owner, device.Device.Id);
    }

    private static object Hrv(DateTimeOffset time, string id = "hc:hrv-1", string revision = "1", double value = 42.5,
        object? workout = null, string metric = "hrvRmssd", string unit = "ms", string method = "ProviderReported",
        string? algorithm = "com.sec.android.app.shealth") => new
        {
            externalId = id,
            revision,
            revisionOrder = long.Parse(revision, CultureInfo.InvariantCulture),
            metric,
            unit,
            value,
            time,
            method,
            providerAlgorithm = algorithm,
            workout,
            native = new { metric = "HeartRateVariabilityRmssdRecord", unit = "ms", value, mappingVersion = "hc-science-v1" },
        };

    private static object Rr(DateTimeOffset start, string revision = "1", double value = 800, object? workout = null,
        string metric = "rrInterval", string unit = "ms", int count = 120) => new
        {
            source = "health-connect",
            externalId = "rr:segment-1",
            revision,
            revisionOrder = long.Parse(revision, CultureInfo.InvariantCulture),
            metric,
            unit,
            method = "Measured",
            workout,
            samples = Enumerable.Range(0, count).Select(i => new { time = start.AddMilliseconds(i * 800.0), value }).ToArray(),
        };

    [Fact]
    public async Task Inventory_reports_actual_rows_and_keeps_other_athletes_out()
    {
        var (user, phone, _, device) = await PhoneAsync();
        var (otherUser, otherPhone, _, _) = await PhoneAsync();
        var sample = Hrv(Yesterday, metric: "spo2", unit: "%", value: 97);
        var body = new { source = "health-connect", samples = new[] { sample } };
        (await phone.PostAsJsonAsync(Observations, body, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await phone.PostAsJsonAsync(Observations, body, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await phone.PostAsJsonAsync(Observations, new { source = "health-connect",
            samples = new[] { Hrv(Yesterday, metric: "spo2", unit: "%", revision: "2", value: 98) } }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await phone.PostAsJsonAsync(Signals, Rr(Yesterday), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await otherPhone.PostAsJsonAsync(Observations, new { source = "health-connect", samples = new[] { Hrv(Yesterday) } }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        const string inventoryUri = "/api/v1/science/inventory";
        var response = await user.GetAsync(inventoryUri, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var inventory = (await response.Content.ReadFromJsonAsync<ScientificInventory>(Json.Options, Ct))!;
        var group = inventory.Observations.Should().ContainSingle().Subject;
        group.Metric.Should().Be("spo2");
        group.DeviceId.Should().Be(device);
        group.RevisionRows.Should().Be(2);
        group.NonNullRows.Should().Be(2);
        inventory.Signals.Should().ContainSingle().Which.StoredSamples.Should().Be(120);
        var other = (await otherUser.GetFromJsonAsync<ScientificInventory>(inventoryUri, Json.Options, Ct))!;
        other.Observations.Should().ContainSingle().Which.Metric.Should().Be("hrvRmssd");
        other.Signals.Should().BeEmpty();

        const string historyUri = inventoryUri + "/observations?metric=spo2&source=health-connect&limit=1";
        var first = (await user.GetFromJsonAsync<InventoryObservationPage>(historyUri, Json.Options, Ct))!;
        first.HasMore.Should().BeTrue();
        var item = first.Items.Should().ContainSingle().Subject;
        var next = historyUri + "&asOf=" + Uri.EscapeDataString(first.AsOf.ToString("O", CultureInfo.InvariantCulture)) +
            "&beforeTime=" + Uri.EscapeDataString(item.Time.ToString("O", CultureInfo.InvariantCulture)) +
            "&beforeId=" + item.Id;
        var second = (await user.GetFromJsonAsync<InventoryObservationPage>(next, Json.Options, Ct))!;
        second.HasMore.Should().BeFalse();
        second.Items.Should().ContainSingle().Which.Id.Should().NotBe(item.Id);
        (await otherUser.GetFromJsonAsync<InventoryObservationPage>(historyUri, Json.Options, Ct))!.Items.Should().BeEmpty();
        (await user.GetAsync(historyUri + "&limit=101", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync(inventoryUri, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await phone.GetAsync(inventoryUri, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Phone_observations_are_imported_once_with_device_provenance_and_reach_the_daily_engine()
    {
        var (user, phone, owner, device) = await PhoneAsync();
        var body = new { source = "health-connect", samples = new[] { Hrv(Yesterday) } };

        var first = await phone.PostAsJsonAsync(Observations, body, Ct);
        var replay = await phone.PostAsJsonAsync(Observations, body, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<IngestObservationsResult>(Json.Options, Ct))!.Imported.Should().Be(1);
        (await replay.Content.ReadFromJsonAsync<IngestObservationsResult>(Json.Options, Ct))!.Imported.Should().Be(0);
        first.Headers.CacheControl!.NoStore.Should().BeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        var row = await db.Set<ScientificObservationRow>().AsNoTracking().SingleAsync(r => r.OwnerId == owner, Ct);
        row.ImportDeviceId.Should().Be(device);
        row.NativeMetric.Should().Be("HeartRateVariabilityRmssdRecord");
        row.MappingVersion.Should().Be("hc-science-v1");
        row.ProviderAlgorithm.Should().Be("com.sec.android.app.shealth");
        row.Method.Should().Be(nameof(ObservationMethod.ProviderReported));

        var day = DateOnly.FromDateTime(Yesterday.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var report = await user.GetFromJsonAsync<DailyMetricReport>(
            $"/api/v1/science/daily?metric=hrvRmssd&source=health-connect&day={day}&baselineDays=7", Json.Options, Ct);
        report!.Current.Value.Should().Be(42.5);
        report.Current.ObservationId.Should().Be(row.Id);
    }

    [Fact]
    public async Task Same_revision_with_different_value_is_a_conflict_and_a_new_revision_is_kept_beside_the_old()
    {
        var (_, phone, owner, _) = await PhoneAsync();
        await phone.PostAsJsonAsync(Observations, new { source = "health-connect", samples = new[] { Hrv(Yesterday) } }, Ct);

        var conflict = await phone.PostAsJsonAsync(Observations,
            new { source = "health-connect", samples = new[] { Hrv(Yesterday, value: 50) } }, Ct);
        var revised = await phone.PostAsJsonAsync(Observations,
            new { source = "health-connect", samples = new[] { Hrv(Yesterday, revision: "2", value: 50) } }, Ct);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        revised.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        (await db.Set<ScientificObservationRow>().CountAsync(r => r.OwnerId == owner, Ct)).Should().Be(2);
    }

    [Fact]
    public async Task Only_phone_tokens_may_ingest_scientific_data()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (scale, _) = await factory.CreateDeviceClientAsync(user);
        var body = new { source = "health-connect", samples = new[] { Hrv(Yesterday) } };

        (await scale.PostAsJsonAsync(Observations, body, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scale.PostAsJsonAsync(Signals, Rr(Yesterday), Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsJsonAsync(Observations, body, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync(Observations, body, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_observations_are_rejected_field_by_field_without_writing()
    {
        var (_, phone, owner, _) = await PhoneAsync();
        var future = factory.Time.GetUtcNow().AddMinutes(5);
        var body = new
        {
            source = "health-connect",
            samples = new[]
            {
                Hrv(Yesterday, id: "a", metric: "hrvRmssd", unit: "s"),
                Hrv(Yesterday, id: "b", metric: "rpe", unit: "0-10"),
                Hrv(Yesterday, id: "c", method: "SelfReported"),
                Hrv(Yesterday, id: "d", metric: "vo2max", unit: "mL/kg/min", method: "ProviderEstimated", algorithm: null),
                Hrv(future, id: "e"),
                Hrv(Yesterday, id: "f"),
            },
        };

        var response = await phone.PostAsJsonAsync(Observations, body, Ct);
        var unsupported = await phone.PostAsJsonAsync(Observations, new { source = "fitbit", samples = new[] { Hrv(Yesterday) } }, Ct);
        var empty = await phone.PostAsJsonAsync(Observations, new { source = "health-connect", samples = Array.Empty<object>() }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        problem!.Errors.Keys.Should().BeEquivalentTo(
            ["samples[0].metric", "samples[1].metric", "samples[2].method", "samples[3].providerAlgorithm", "samples[4].time"]);
        (await unsupported.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.Keys.Should().Contain("source");
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        (await db.Set<ScientificObservationRow>().AnyAsync(r => r.OwnerId == owner, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Workout_references_resolve_only_to_the_athletes_own_workouts()
    {
        var (_, phone, owner, _) = await PhoneAsync();
        var (_, strangerPhone, _, _) = await PhoneAsync();
        var start = factory.Time.GetUtcNow().AddHours(-3);
        (await phone.PostAsJsonAsync("/api/v1/ingest/workouts", WorkoutIngestionTests.Payload(start, "run:0"), Ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var reference = new { source = "samsung-health", sourceId = "run:0" };
        var sample = Hrv(start.AddMinutes(30), id: "hc:hr-run", metric: "heartRate", unit: "bpm", workout: reference);

        var linked = await phone.PostAsJsonAsync(Observations, new { source = "health-connect", samples = new[] { sample } }, Ct);
        var foreign = await strangerPhone.PostAsJsonAsync(Observations, new { source = "health-connect", samples = new[] { sample } }, Ct);
        var unknown = await phone.PostAsJsonAsync(Observations, new
        {
            source = "health-connect",
            samples = new[] { Hrv(start, id: "x", workout: new { source = "samsung-health", sourceId = "missing:0" }) },
        }, Ct);

        linked.StatusCode.Should().Be(HttpStatusCode.OK);
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        var workout = await db.Workouts.AsNoTracking().SingleAsync(w => w.OwnerId == owner, Ct);
        (await db.Set<ScientificObservationRow>().AsNoTracking().SingleAsync(r => r.OwnerId == owner, Ct))
            .WorkoutId.Should().Be(workout.Id);
    }

    [Fact]
    public async Task Paged_import_advances_the_checkpoint_atomically_and_rejects_stale_cursors()
    {
        var (_, phone, _, _) = await PhoneAsync();
        const string cursorUri = "/api/v1/ingest/observations/cursor?source=garmin-connect&stream=daily";
        (await phone.GetFromJsonAsync<SyncCursorDto>(cursorUri, Json.Options, Ct))!.Cursor.Should().BeNull();
        object Page(string? previous, string next, params object[] samples) =>
            new { source = "garmin-connect", page = new { stream = "daily", previousCursor = previous, nextCursor = next }, samples };

        var first = await phone.PostAsJsonAsync(Observations, Page(null, "c1", Hrv(Yesterday, id: "g:1")), Ct);
        var replay = await phone.PostAsJsonAsync(Observations, Page(null, "c1", Hrv(Yesterday, id: "g:1")), Ct);
        var stale = await phone.PostAsJsonAsync(Observations, Page(null, "c2", Hrv(Yesterday, id: "g:2")), Ct);
        var next = await phone.PostAsJsonAsync(Observations, Page("c1", "c2"), Ct);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<IngestObservationsResult>(Json.Options, Ct))!.Cursor.Should().Be("c1");
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        next.StatusCode.Should().Be(HttpStatusCode.OK);
        (await phone.GetFromJsonAsync<SyncCursorDto>(cursorUri, Json.Options, Ct))!.Cursor.Should().Be("c2");
    }

    [Fact]
    public async Task Signal_chunks_are_idempotent_conflict_on_changed_content_and_reach_the_rr_reader()
    {
        var (user, phone, owner, device) = await PhoneAsync();
        var start = Yesterday;

        var first = await phone.PostAsJsonAsync(Signals, Rr(start), Ct);
        var replay = await phone.PostAsJsonAsync(Signals, Rr(start), Ct);
        var changed = await phone.PostAsJsonAsync(Signals, Rr(start, value: 810), Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await first.Content.ReadFromJsonAsync<IngestSignalResult>(Json.Options, Ct))!;
        created.SampleCount.Should().Be(120);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<IngestSignalResult>(Json.Options, Ct))!.Id.Should().Be(created.Id);
        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        var row = await db.Set<ScientificSignalChunkRow>().AsNoTracking().SingleAsync(r => r.OwnerId == owner, Ct);
        row.Id.Should().Be(created.Id);
        row.DeviceId.Should().Be(device);
        var day = DateOnly.FromDateTime(start.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var report = await user.GetFromJsonAsync<RrChunkReport>(
            $"/api/v1/science/rr?source=health-connect&day={day}&context=resting&screened=true&timestampsAreBeatEnds=true&chunkId={created.Id}",
            Json.Options, Ct);
        report!.Selected!.Id.Should().Be(created.Id);
        report.Analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task Invalid_signal_chunks_are_rejected()
    {
        var (_, phone, _, _) = await PhoneAsync();
        var start = Yesterday;
        var unordered = new
        {
            source = "health-connect",
            externalId = "rr:x",
            revision = "1",
            metric = "rrInterval",
            unit = "ms",
            method = "Measured",
            samples = new[] { new { time = start.AddSeconds(1), value = 800.0 }, new { time = start, value = 800.0 } },
        };

        (await phone.PostAsJsonAsync(Signals, unordered, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await phone.PostAsJsonAsync(Signals, Rr(start, metric: "sleepDuration", unit: "s"), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await phone.PostAsJsonAsync(Signals, Rr(start, metric: "rrInterval", unit: "s"), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await phone.PostAsJsonAsync(Signals, Rr(start, count: 0), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await phone.PostAsJsonAsync(Signals, Rr(factory.Time.GetUtcNow().AddSeconds(-10)), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await phone.PostAsJsonAsync(Signals, Rr(start, workout: new { source = "samsung-health", sourceId = "none:0" }), Ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
