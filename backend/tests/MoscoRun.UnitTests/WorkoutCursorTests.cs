using MoscoRun.Api.Features.Workouts;

namespace MoscoRun.UnitTests;

public sealed class WorkoutCursorTests
{
    [Fact]
    public void Cursor_round_trips_exactly()
    {
        var cursor = new WorkoutCursor(new DateTimeOffset(2026, 11, 16, 7, 30, 15, TimeSpan.Zero).AddTicks(1234560), Guid.NewGuid());

        WorkoutCursor.TryParse(cursor.ToString(), out var parsed).Should().BeTrue();

        parsed.Should().Be(cursor);
    }

    [Fact]
    public void Cursor_is_url_safe()
    {
        var text = new WorkoutCursor(DateTimeOffset.MaxValue, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")).ToString();

        text.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        Uri.EscapeDataString(text).Should().Be(text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("not base64 at all, definitely!!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("f_______________________________")]
    public void Malformed_cursor_is_rejected_without_throwing(string? text)
    {
        WorkoutCursor.TryParse(text, out _).Should().BeFalse();
    }
}
