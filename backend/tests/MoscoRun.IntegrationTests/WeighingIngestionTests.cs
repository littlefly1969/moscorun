using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Api.Features.Weighings;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class WeighingIngestionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private object Payload(string id, decimal kg = 78.4m, int? ohm = 480, DateTimeOffset? at = null) => new
    {
        sourceEventId = id,
        measuredAt = at ?? factory.Time.GetUtcNow().AddMinutes(-2),
        weightKg = kg,
        impedanceOhm = ohm,
    };

    [Fact]
    public async Task First_delivery_creates_and_identical_replay_returns_same_record()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, device) = await factory.CreateDeviceClientAsync(user);
        var payload = Payload("evt-1");

        var first = await agent.PostAsJsonAsync("/api/v1/ingest/weighings", payload, Ct);
        var replay = await agent.PostAsJsonAsync("/api/v1/ingest/weighings", payload, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await first.Content.ReadFromJsonAsync<WeighingDto>(Json.Options, Ct);
        var replayed = await replay.Content.ReadFromJsonAsync<WeighingDto>(Json.Options, Ct);
        replayed!.Id.Should().Be(created!.Id);
        created.DeviceId.Should().Be(device.Device.Id);
        first.Headers.Location!.ToString().Should().Be($"/api/v1/weighings/{created.Id}");
    }

    [Fact]
    public async Task Same_event_with_different_values_is_a_conflict()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, _) = await factory.CreateDeviceClientAsync(user);
        await agent.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("evt-c", 78.4m), Ct);

        var conflict = await agent.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("evt-c", 81m), Ct);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem!.Extensions.Should().ContainKey("existingId");
    }

    [Fact]
    public async Task Same_event_id_from_two_devices_creates_two_weighings()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (a, _) = await factory.CreateDeviceClientAsync(user, "A");
        var (b, _) = await factory.CreateDeviceClientAsync(user, "B");

        (await a.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("shared"), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await b.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("shared"), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Concurrent_deliveries_of_one_event_store_exactly_one_row()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, device) = await factory.CreateDeviceClientAsync(user);
        var payload = Payload("evt-race");

        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => agent.PostAsJsonAsync("/api/v1/ingest/weighings", payload, Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(11);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>();
        (await db.Weighings.CountAsync(w => w.DeviceId == device.Device.Id, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Invalid_reading_returns_field_errors_and_codes()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, _) = await factory.CreateDeviceClientAsync(user);

        var response = await agent.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("bad id", kg: 500m), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        problem!.Errors.Keys.Should().BeEquivalentTo(["sourceEventId", "weightKg"]);
    }

    [Fact]
    public async Task Missing_required_fields_are_rejected()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, _) = await factory.CreateDeviceClientAsync(user);

        var response = await agent.PostAsJsonAsync("/api/v1/ingest/weighings", new { sourceEventId = "x" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ingestion_requires_a_valid_active_device_token()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, device) = await factory.CreateDeviceClientAsync(user);

        // Un utente con cookie non può fingersi agente.
        (await user.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("u"), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var forged = factory.CreateClient();
        forged.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", device.Token[..^4] + "AAAA");
        (await forged.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("f"), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await user.DeleteAsync($"/api/v1/devices/{device.Device.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await agent.PostAsJsonAsync("/api/v1/ingest/weighings", Payload("r"), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Device_token_cannot_read_user_data()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (agent, _) = await factory.CreateDeviceClientAsync(user);
        (await agent.GetAsync("/api/v1/weighings", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
