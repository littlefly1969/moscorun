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

    private async Task<(HttpClient User, HttpClient Phone, List<Guid> NewestFirst)> SeedManyAsync(int count)
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, _) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            // Dal più recente al più vecchio, anche oltre i 90 giorni della finestra storica.
            ids.Add(await IngestAsync(phone, factory.Time.GetUtcNow().AddDays(-1 - (i * 20)), $"many:{i}"));
        }

        return (user, phone, ids);
    }

    private static async Task<Guid> IngestAsync(HttpClient phone, DateTimeOffset start, string sourceId)
    {
        var response = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", new
        {
            source = "samsung-health",
            sourceId,
            revision = 1,
            activityType = "RUNNING",
            startedAt = start,
            endedAt = start.AddMinutes(30),
        }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, Ct))!.Id;
    }

    private static async Task<List<Guid>> ReadAllPagesAsync(HttpClient client, string query, Func<int, Task>? betweenPages = null)
    {
        var ids = new List<Guid>();
        string? cursor = null;
        for (var page = 0; page < 100; page++)
        {
            var url = $"/api/v1/workouts?{query}{(cursor is null ? string.Empty : $"&cursor={cursor}")}";
            var list = (await client.GetFromJsonAsync<WorkoutList>(url, Json.Options, Ct))!;
            ids.AddRange(list.Items.Select(w => w.Id));
            cursor = list.NextCursor;
            if (cursor is null)
            {
                return ids;
            }

            if (betweenPages is not null)
            {
                await betweenPages(page);
            }
        }

        throw new InvalidOperationException("La paginazione non termina.");
    }

    [Fact]
    public async Task Paged_list_walks_the_whole_history_in_stable_order()
    {
        var (user, _, ids) = await SeedManyAsync(7);

        var first = (await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts?pageSize=3", Json.Options, Ct))!;
        var all = await ReadAllPagesAsync(user, "pageSize=3");

        first.Items.Select(w => w.Id).Should().Equal(ids.Take(3));
        first.NextCursor.Should().NotBeNullOrEmpty();
        first.From.Should().BeNull("la lista paginata non applica la finestra di 90 giorni");
        all.Should().Equal(ids, "sette allenamenti su 121 giorni, tutti e nell'ordine dal più recente");
    }

    [Fact]
    public async Task Last_page_has_no_cursor_even_when_exactly_full()
    {
        var (user, _, ids) = await SeedManyAsync(4);

        var first = (await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts?pageSize=2", Json.Options, Ct))!;
        var second = (await user.GetFromJsonAsync<WorkoutList>($"/api/v1/workouts?pageSize=2&cursor={first.NextCursor}", Json.Options, Ct))!;

        second.Items.Select(w => w.Id).Should().Equal(ids.Skip(2));
        second.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Workouts_added_or_removed_while_scrolling_do_not_shift_the_pages()
    {
        var (user, phone, ids) = await SeedManyAsync(6);
        var added = new List<Guid>();

        var all = await ReadAllPagesAsync(user, "pageSize=2", async page =>
        {
            // Uno più recente di tutti (prima della posizione corrente) e uno in mezzo a quelli ancora da leggere.
            added.Add(await IngestAsync(phone, factory.Time.GetUtcNow().AddHours(-1 - page), $"newer:{page}"));
            if (page == 0)
            {
                added.Add(await IngestAsync(phone, factory.Time.GetUtcNow().AddDays(-70), "middle:0"));
            }
        });

        all.Should().OnlyHaveUniqueItems();
        all.Should().Contain(ids, "nessun allenamento presente all'inizio viene saltato");
        all.Should().Contain(added[1], "quello inserito più avanti nello scorrimento viene incontrato");
        all.Where(ids.Contains).Should().Equal(ids);
    }

    [Fact]
    public async Task Workouts_starting_at_the_same_instant_are_neither_skipped_nor_repeated()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, _) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var start = factory.Time.GetUtcNow().AddDays(-3);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await IngestAsync(phone, start, $"same:{i}"));
        }

        var all = await ReadAllPagesAsync(user, "pageSize=2");

        all.Should().BeEquivalentTo(ids);
        all.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Paged_list_honours_from_and_to()
    {
        var (user, _, ids) = await SeedManyAsync(6);
        var from = Uri.EscapeDataString(factory.Time.GetUtcNow().AddDays(-70).ToString("O"));
        var to = Uri.EscapeDataString(factory.Time.GetUtcNow().AddDays(-10).ToString("O"));

        var all = await ReadAllPagesAsync(user, $"pageSize=1&from={from}&to={to}");

        all.Should().Equal(ids.Skip(1).Take(3), "allenamenti a 21, 41 e 61 giorni");
    }

    [Fact]
    public async Task List_without_paging_parameters_keeps_the_previous_contract()
    {
        var (user, _, ids) = await SeedManyAsync(6);

        var list = (await user.GetFromJsonAsync<WorkoutList>("/api/v1/workouts", Json.Options, Ct))!;

        list.Items.Select(w => w.Id).Should().Equal(ids.Take(5), "finestra predefinita di 90 giorni");
        list.NextCursor.Should().BeNull();
        list.From.Should().Be(list.To.AddDays(-90));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    [InlineData("pageSize=-1")]
    [InlineData("cursor=")]
    [InlineData("cursor=abc")]
    [InlineData("pageSize=10&cursor=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA%3D")]
    public async Task Invalid_paging_parameters_are_rejected(string query)
    {
        var (user, _) = await factory.CreateUserClientAsync();

        (await user.GetAsync($"/api/v1/workouts?{query}", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cursor_of_another_person_never_reveals_their_workouts()
    {
        var (owner, _, _) = await SeedManyAsync(3);
        var cursor = (await owner.GetFromJsonAsync<WorkoutList>("/api/v1/workouts?pageSize=1", Json.Options, Ct))!.NextCursor;
        var (stranger, _) = await factory.CreateUserClientAsync();
        var (strangerPhone, _) = await factory.CreateDeviceClientAsync(stranger, "Altro", DeviceKind.PhoneBridge);

        var list = (await strangerPhone.GetFromJsonAsync<WorkoutList>($"/api/v1/workouts?pageSize=50&cursor={cursor}", Json.Options, Ct))!;

        list.Items.Should().BeEmpty();
        list.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Phone_token_can_page_through_workouts()
    {
        var (_, phone, ids) = await SeedManyAsync(3);

        (await ReadAllPagesAsync(phone, "pageSize=2")).Should().Equal(ids);
    }

    [Fact]
    public async Task Single_metric_can_be_requested_and_reduced_for_charts()
    {
        var (user, _, workout) = await SeedAsync(points: 3600);
        var url = $"/api/v1/workouts/{workout.Id}/series";

        var hr = (await user.GetFromJsonAsync<WorkoutSeriesList>($"{url}?metric=HeartRate&maxPoints=300", Json.Options, Ct))!;
        var full = (await user.GetFromJsonAsync<WorkoutSeriesList>($"{url}?metric=HeartRate", Json.Options, Ct))!;
        var absent = (await user.GetFromJsonAsync<WorkoutSeriesList>($"{url}?metric=Power", Json.Options, Ct))!;
        var everything = (await user.GetFromJsonAsync<WorkoutSeriesList>($"{url}?maxPoints=100", Json.Options, Ct))!;

        var reduced = hr.Items.Should().ContainSingle().Subject;
        reduced.Metric.Should().Be(SeriesMetric.HeartRate);
        reduced.TotalSamples.Should().Be(3600);
        reduced.Times.Count.Should().BeLessThanOrEqualTo(300).And.Be(reduced.Values.Count);
        reduced.Times.Should().BeInAscendingOrder();
        reduced.Values.Max().Should().Be(full.Items[0].Values.Max(), "il massimo reale resta visibile nel grafico");
        reduced.Values.Min().Should().Be(full.Items[0].Values.Min());
        reduced.Times[0].Should().Be(full.Items[0].Times[0]);
        reduced.Times[^1].Should().Be(full.Items[0].Times[^1]);
        full.Items[0].Values.Should().HaveCount(3600);
        full.Items[0].TotalSamples.Should().Be(3600);
        absent.Items.Should().BeEmpty();
        everything.Items.Should().HaveCount(2).And.OnlyContain(s => s.Values.Count <= 100 && s.TotalSamples == 3600);
    }

    [Theory]
    [InlineData("maxPoints=1")]
    [InlineData("maxPoints=100001")]
    [InlineData("metric=Sleep")]
    [InlineData("metric=99")]
    public async Task Invalid_series_parameters_are_rejected(string query)
    {
        var (user, _, workout) = await SeedAsync(points: 60);

        (await user.GetAsync($"/api/v1/workouts/{workout.Id}/series?{query}", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
