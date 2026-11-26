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
        var store = scope.ServiceProvider.GetRequiredService<AthleteFeedbackStore>();
        (await store.SaveAsync(owner, "entry", "1", time, [new("rpe", "0-10", 7), new("hunger", "0-10", null)], ct)).Should().Be(2);
        Func<Task> invalid = () => store.SaveAsync(owner, "entry", "2", time, [new("rpe", "0-10", 11)], ct);
        await invalid.Should().ThrowAsync<ArgumentException>();
        var result = await scope.ServiceProvider.GetRequiredService<ScientificObservationStore>()
            .ReadAsOfAsync(owner, factory.Time.GetUtcNow(), ct);
        result.Should().OnlyContain(o => o.Method == ObservationMethod.SelfReported);
        result.Single(o => string.Equals(o.Metric, "hunger", StringComparison.Ordinal)).Value.Should().BeNull();
    }

    [Fact]
    public async Task Text_and_values_are_versioned_atomically_and_owner_scoped()
    {
        var (_, owner) = await factory.CreateUserAsync();
        var (_, other) = await factory.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var time = factory.Time.GetUtcNow().AddHours(-1);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<AthleteFeedbackStore>();
        var query = scope.ServiceProvider.GetRequiredService<AthleteFeedbackQuery>();
        var entry = new AthleteFeedbackEntry("after-run", "1", time, [new("rpe", "0-10", 6)], RevisionOrder: 1, Note: "Test.");
        (await store.SaveEntryAsync(owner, entry, ct)).Should().Be(new AthleteFeedbackSaveResult(true, 1));
        (await store.SaveEntryAsync(owner, entry, ct)).Should().Be(new AthleteFeedbackSaveResult(false, 0));
        Func<Task> changed = () => store.SaveEntryAsync(owner, entry with { Note = "Modificata." }, ct);
        await changed.Should().ThrowAsync<InvalidOperationException>();
        var correction = entry with { Revision = "2", RevisionOrder = 2, Values = [], Note = "Solo nota." };
        (await store.SaveEntryAsync(owner, correction, ct)).Should().Be(new AthleteFeedbackSaveResult(true, 0));
        (await query.ReadHistoryAsync(owner, entry.EntryId, time, ct)).Should().BeEmpty();
        (await query.ReadHistoryAsync(other, entry.EntryId, factory.Time.GetUtcNow(), ct)).Should().BeEmpty();
        var history = await query.ReadHistoryAsync(owner, entry.EntryId, factory.Time.GetUtcNow(), ct);
        history.Should().HaveCount(2);
        history.Single(x => x.Entry.RevisionOrder == 2).Entry.Note.Should().Be("Solo nota.");
    }
}
