using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class SleepDiaryEngineTests
{
    private static readonly DateOnly Last = new(2026, 11, 25);
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Midnight_wrapping_and_efficiency_use_only_confirmed_complete_nights()
    {
        var nights = Enumerable.Range(0, 8).Select(index => Night(Last.AddDays(index - 7),
            index % 2 == 0 ? 1430 : 10, confirmed: index != 7)).ToArray();
        var report = SleepDiaryEngine.Build(Last, 8, Now, nights)!;
        report.CompleteNights.Should().Be(7);
        report.Status.Should().Be("observed");
        var mean = report.Bedtime!.MeanMinuteOfDay;
        Math.Min(mean, 1440 - mean).Should().BeLessThan(3);
        report.MeanEfficiencyPercent.Should().Be(87.5);
        report.Nights[^1].BedtimeMinute.Should().BeNull();
    }

    [Fact]
    public void Invalid_duration_and_insufficient_support_do_not_produce_summary()
    {
        var nights = Enumerable.Range(0, 7).Select(index => Night(Last.AddDays(index - 6), 1430, true)).ToArray();
        nights[0] = SleepDiaryEngine.Night(nights[0].Day, 1, Now, false, Values(1430, true, 30000));
        nights[0].Status.Should().Be("invalid-duration");
        var report = SleepDiaryEngine.Build(Last, 7, Now, nights)!;
        report.Bedtime.Should().BeNull();
        report.MeanEfficiencyPercent.Should().BeNull();
        SleepDiaryMetrics.ValidValue(SleepDiaryMetrics.Bedtime, 1440).Should().BeFalse();
    }

    private static SleepDiaryNight Night(DateOnly day, double bed, bool confirmed) =>
        SleepDiaryEngine.Night(day, 1, Now, false, Values(bed, confirmed, 25200));

    private static Dictionary<string, double?> Values(double bed, bool confirmed, double asleep) =>
        new(StringComparer.Ordinal)
        {
            [SleepDiaryMetrics.Bedtime] = bed,
            [SleepDiaryMetrics.Wake] = 480,
            [SleepDiaryMetrics.InBed] = 28800,
            [SleepDiaryMetrics.Confirmed] = confirmed ? 1 : 0,
            ["sleepDuration"] = asleep,
        };
}
