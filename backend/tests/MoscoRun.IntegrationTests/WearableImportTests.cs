using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Api.Features.Workouts;
using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class WearableImportTests(ApiFactory factory)
{
    [Fact]
    public async Task Imports_multiple_sources_without_merging_them()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var import = scope.ServiceProvider.GetRequiredService<WearableImport>();
        var sample = new WearableSample("sample-1", "1", "hrvRmssd", "ms", null,
            factory.Time.GetUtcNow().AddHours(-1), ObservationMethod.ProviderReported);
        (await import.ImportAsync(owner, "garmin-connect", [sample], ct)).Should().Be(1);
        (await import.ImportAsync(owner, "samsung-health", [sample], ct)).Should().Be(1);
        (await import.ImportAsync(owner, "garmin-connect", [sample], ct)).Should().Be(0);
        var result = await scope.ServiceProvider.GetRequiredService<ScientificObservationStore>()
            .ReadAsOfAsync(owner, factory.Time.GetUtcNow(), ct);
        result.Should().HaveCount(2);
        result.Should().OnlyContain(o => o.Value == null);
    }

    [Fact]
    public async Task Native_values_workout_device_and_cursor_are_preserved_and_owner_scoped()
    {
        var (user, owner) = await factory.CreateUserClientAsync();
        using var userClient = user;
        var (phone, registered) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);
        using var phoneClient = phone;
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddHours(-2);
        using var response = await phone.PostAsJsonAsync("/api/v1/ingest/workouts", WorkoutIngestionTests.Payload(time), ct);
        response.EnsureSuccessStatusCode();
        var workout = (await response.Content.ReadFromJsonAsync<WorkoutSummaryDto>(Json.Options, ct))!;
        var provenance = new ScientificImportProvenance(registered.Device.Id, workout.Id, "fit:verticalOscillation",
            "mm", 85, "wearable-mapping-v1");
        var sample = new WearableSample("sample-1", "1", "verticalOscillation", "m", 0.085, time,
            ObservationMethod.ProviderReported, RevisionOrder: 1, Provenance: provenance);
        await using var scope = factory.Services.CreateAsyncScope();
        var import = scope.ServiceProvider.GetRequiredService<WearableImport>();
        (await import.ImportPageAsync(owner, "garmin-connect", "account-1", null, "c1", [sample], ct)).Should().Be(1);
        (await import.ImportPageAsync(owner, "garmin-connect", "account-1", null, "c1", [sample], ct)).Should().Be(0);
        var reader = scope.ServiceProvider.GetRequiredService<ScientificRevisionQuery>();
        var rows = await reader.ReadHistoryAsync(owner, "garmin-connect", "sample-1", "verticalOscillation", factory.Time.GetUtcNow(), ct);
        rows.Should().ContainSingle().Which.Provenance.Should().Be(provenance);
        Func<Task> foreign = () => import.ImportAsync(other, "garmin-connect", [sample], ct);
        await foreign.Should().ThrowAsync<ArgumentException>();
        (await import.ReadCursorAsync(other, "garmin-connect", "account-1", ct)).Should().BeNull();
    }
}
