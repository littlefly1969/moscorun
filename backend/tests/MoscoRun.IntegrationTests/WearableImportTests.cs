using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
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
        var import = new WearableImport(new ScientificImportStore(scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>(), factory.Time));
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
}
