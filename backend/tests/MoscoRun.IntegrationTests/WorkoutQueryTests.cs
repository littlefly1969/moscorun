using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Devices;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.IntegrationTests;

public sealed class WorkoutQueryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient User, HttpClient Phone, WorkoutSummaryDto Workout)> SeedAsync(int points = 3600)
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, _) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var response = await phone.PostAsJsonAsync(
            "/api/v1/ingest/workouts", WorkoutIngestionTests.Payload(factory.Time.GetUtcNow().AddDays(-1), points: points), Ct);
        response.EnsureSuccessStatusCode();
        return (user, phone, (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!);
    }

    [Fact]
    public async Task List_returns_summaries_in_range_newest_first()
    {
        var (user, phone, workout) = await SeedAsync(points: 60);
        var older = WorkoutIngestionTests.Payload(factory.Time.GetUtcNow().AddDays(-10), sourceId: "older:0", points: 60);
        (await phone.PostAsJsonAsync("/api/v1/ingest/workouts", older, Ct)).EnsureSuccessStatusCode();

        var all = await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct);
        var recent = await user.GetFromJsonAsync<WorkoutList>(
            $"/api/v1/workouts?from={Uri.EscapeDataString(factory.Time.GetUtcNow().AddDays(-2).ToString("O"))}", Json.Options, Ct);

        all!.Items.Should().HaveCount(2);
        all.Items[0].Id.Should().Be(workout.Id);
        recent!.Items.Should().ContainSingle().Which.Id.Should().Be(workout.Id);
        (await user.GetAsync("/api/v1/workouts?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z", Ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Detail_lists_available_series_with_units()
    {
        var (user, _, workout) = await SeedAsync(points: 60);

        var detail = await user.GetFromJsonAsync<WorkoutDetailDto>($"/api/v1/workouts/{workout.Id}", Json.Options, Ct);

        detail!.Summary.Id.Should().Be(workout.Id);
        detail.Series.Should().BeEquivalentTo(
            [new SeriesInfoDto(SeriesMetric.HeartRate, "bpm", 60), new SeriesInfoDto(SeriesMetric.Speed, "m/s", 60)]);
    }

    [Fact]
    public async Task Route_is_geojson_simplified_to_the_requested_size()
    {
        var (user, _, workout) = await SeedAsync();

        var full = await user.GetFromJsonAsync<RouteFeature>($"/api/v1/workouts/{workout.Id}/route?maxPoints=100000", Json.Options, Ct);
        var light = await user.GetFromJsonAsync<RouteFeature>($"/api/v1/workouts/{workout.Id}/route?maxPoints=250", Json.Options, Ct);
        var standard = await user.GetFromJsonAsync<RouteFeature>($"/api/v1/workouts/{workout.Id}/route", Json.Options, Ct);

        full!.Type.Should().Be("Feature");
        full.Geometry.Type.Should().Be("LineString");
        full.Geometry.Coordinates.Should().HaveCount(3600);
        full.Properties.Times.Should().HaveCount(3600);
        full.Geometry.Coordinates[0].Should().HaveCount(2, "il primo punto non ha altitudine");
        full.Geometry.Coordinates[1].Should().HaveCount(3);
        full.Geometry.Coordinates[1][0].Should().BeInRange(12, 13, "GeoJSON vuole [longitudine, latitudine]");
        full.Bbox.Should().HaveCount(4);
        light!.Geometry.Coordinates.Should().HaveCount(250);
        light.Properties.ReturnedPoints.Should().Be(250);
        light.Properties.TotalPoints.Should().Be(3600);
        light.Geometry.Coordinates[0].Should().Equal(full.Geometry.Coordinates[0]);
        light.Geometry.Coordinates[^1].Should().Equal(full.Geometry.Coordinates[^1]);
        standard!.Geometry.Coordinates.Should().HaveCount(WorkoutEndpoints.DefaultRoutePoints);
    }

    [Fact]
    public async Task Route_supports_conditional_requests()
    {
        var (user, _, workout) = await SeedAsync(points: 60);
        var url = $"/api/v1/workouts/{workout.Id}/route";

        var first = await user.GetAsync(url, Ct);
        first.Headers.ETag.Should().NotBeNull();
        first.Headers.CacheControl!.Private.Should().BeTrue();

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await user.SendAsync(request, Ct);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100_001)]
    public async Task Route_rejects_out_of_range_sizes(int maxPoints)
    {
        var (user, _, workout) = await SeedAsync(points: 60);

        (await user.GetAsync($"/api/v1/workouts/{workout.Id}/route?maxPoints={maxPoints}", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Workout_without_gps_has_no_route()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, _) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var start = factory.Time.GetUtcNow().AddDays(-1);
        var response = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId = "indoor:0",
            revision = 1,
            activityType = "TREADMILL",
            startedAt = start,
            endedAt = start.AddMinutes(30),
        }, Ct);
        var workout = (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!;

        workout.TrackPointCount.Should().Be(0);
        workout.Bounds.Should().BeNull();
        (await user.GetAsync($"/api/v1/workouts/{workout.Id}/route", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await user.GetFromJsonAsync<WorkoutSeriesList>($"/api/v1/workouts/{workout.Id}/series", Json.Options, Ct))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Series_are_returned_with_aligned_times_and_values()
    {
        var (user, _, workout) = await SeedAsync(points: 60);

        var series = await user.GetFromJsonAsync<WorkoutSeriesList>($"/api/v1/workouts/{workout.Id}/series", Json.Options, Ct);

        var hr = series!.Items.Single(s => s.Metric == SeriesMetric.HeartRate);
        hr.Unit.Should().Be("bpm");
        hr.Times.Should().HaveCount(60).And.BeInAscendingOrder();
        hr.Values.Should().HaveCount(60);
        hr.Values[3].Should().Be(143);
    }

    [Fact]
    public async Task Other_people_cannot_see_a_workout()
    {
        var (_, _, workout) = await SeedAsync(points: 60);
        var (stranger, _) = await factory.CreateUserClientAsync();

        foreach (var path in new[] { "", "/route", "/series" })
        {
            (await stranger.GetAsync($"/api/v1/workouts/{workout.Id}{path}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        (await stranger.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Phone_token_can_read_its_owner_workouts_but_scale_and_anonymous_cannot()
    {
        var (user, phone, workout) = await SeedAsync(points: 60);
        var (scale, _) = await factory.CreateDeviceClientAsync(user, "Bilancia", DeviceKind.ScaleAgent);
        var anonymous = factory.CreateClient();

        (await phone.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct))!.Items.Should().ContainSingle();
        (await phone.GetAsync($"/api/v1/workouts/{workout.Id}/route", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await scale.GetAsync("/api/v1/workouts", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.GetAsync("/api/v1/weighings", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "il telefono legge solo gli allenamenti");
        anonymous.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        (await anonymous.GetAsync("/api/v1/workouts", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Phone_token_of_another_person_does_not_see_the_workout()
    {
        var (_, _, workout) = await SeedAsync(points: 60);
        var (otherUser, _) = await factory.CreateUserClientAsync();
        var (otherPhone, _) = await factory.CreateDeviceClientAsync(otherUser, "Altro", DeviceKind.PhoneBridge);

        (await otherPhone.GetAsync($"/api/v1/workouts/{workout.Id}/route", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
