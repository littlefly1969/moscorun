using System.Net;
using System.Net.Http.Json;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class ScienceDiagnosticEndpointTests(ApiFactory factory)
{
    private const string Uri = "/api/v1/ingest/science-diagnostic";
    private const string Inventory = "/api/v1/science/inventory";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ScienceDiagnosticReport Report()
    {
        var now = factory.Time.GetUtcNow();
        return new ScienceDiagnosticReport("health-connect", Guid.NewGuid(), now.AddMinutes(-1), now, "COMPLETED",
            now.AddDays(-7), now.AddMinutes(-5), 1,
            [new ScienceMetricDiagnostic("spo2", "granted", 10, 9, 7, 1, 1),
                new ScienceMetricDiagnostic("vo2max", "denied", 0, 0, 0, 0, 0)]);
    }

    [Fact]
    public async Task Diagnostic_is_isolated_idempotent_and_cannot_be_written_with_a_scale_token()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, device) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var (scale, _) = await factory.CreateDeviceClientAsync(user);
        var (other, _) = await factory.CreateUserClientAsync();
        var report = Report();
        (await phone.PostAsJsonAsync(Uri, report, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await phone.PostAsJsonAsync(Uri, report, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await scale.PostAsJsonAsync(Uri, report, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsJsonAsync(Uri, report, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await phone.PostAsJsonAsync(Uri, report with { State = "MORE_WORK" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var old = report with { RunId = Guid.NewGuid(), StartedAt = report.StartedAt.AddMinutes(-1) };
        (await phone.PostAsJsonAsync(Uri, old, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var result = (await user.GetFromJsonAsync<ScientificInventory>(Inventory, Json.Options, Ct))!;
        var snapshot = result.Diagnostics.Should().ContainSingle().Subject;
        snapshot.DeviceId.Should().Be(device.Device.Id);
        snapshot.Report.Should().BeEquivalentTo(report);
        result.Observations.Should().BeEmpty();
        (await other.GetFromJsonAsync<ScientificInventory>(Inventory, Json.Options, Ct))!.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_counts_and_denied_permission_do_not_create_a_report()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, _) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        var report = Report() with { Metrics = [new ScienceMetricDiagnostic("spo2", "denied", 1, 1, 1, 0, 0)] };
        (await phone.PostAsJsonAsync(Uri, report, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalid = report with { Metrics = [new ScienceMetricDiagnostic("spo2", "granted", 1, 1, 1, 1, 0)] };
        (await phone.PostAsJsonAsync(Uri, invalid, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.GetFromJsonAsync<ScientificInventory>(Inventory, Json.Options, Ct))!.Diagnostics.Should().BeEmpty();
    }
}
