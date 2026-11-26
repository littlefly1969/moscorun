using Microsoft.Extensions.DependencyInjection;
using MoscoRun.Domain.Analysis;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.IntegrationTests;

public sealed class AthleteFeedbackStoreTests(ApiFactory factory)
{
    [Fact]
    public async Task Feedback_preserves_null_and_rejects_invalid_scores()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddMinutes(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = new AthleteFeedbackStore(new ScientificImportStore(scope.ServiceProvider.GetRequiredService<MoscoRunDbContext>(), factory.Time));
        (await store.SaveAsync(owner, "entry", "1", time, [new("rpe", "0-10", 7), new("hunger", "0-10", null)], ct)).Should().Be(2);
        Func<Task> invalid = () => store.SaveAsync(owner, "entry", "2", time, [new("rpe", "0-10", 11)], ct);
        await invalid.Should().ThrowAsync<ArgumentException>();
        var result = await scope.ServiceProvider.GetRequiredService<ScientificObservationStore>()
            .ReadAsOfAsync(owner, factory.Time.GetUtcNow(), ct);
        result.Should().OnlyContain(o => o.Method == ObservationMethod.SelfReported);
        result.Single(o => string.Equals(o.Metric, "hunger", StringComparison.Ordinal)).Value.Should().BeNull();
    }
}
