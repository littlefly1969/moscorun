using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

public sealed class SeriesDownsamplerTests
{
    private static SeriesSample[] Series(int count, Func<int, double> value) =>
        [.. Enumerable.Range(0, count).Select(i => new SeriesSample(TestTracks.Start.AddSeconds(i), value(i)))];

    [Fact]
    public void Short_series_is_returned_unchanged()
    {
        var samples = Series(10, i => i);

        SeriesDownsampler.Downsample(samples, 10).Should().BeSameAs(samples);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(100)]
    [InlineData(999)]
    [InlineData(3599)]
    public void Result_never_exceeds_the_limit_and_keeps_endpoints_and_order(int max)
    {
        var samples = Series(3600, i => 140 + (20 * Math.Sin(i / 37.0)));

        var result = SeriesDownsampler.Downsample(samples, max);

        result.Count.Should().BeLessThanOrEqualTo(max);
        result.Count.Should().BeGreaterThanOrEqualTo(Math.Min(max, 2));
        result[0].Should().Be(samples[0]);
        result[^1].Should().Be(samples[^1]);
        result.Select(s => s.Time).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        result.Should().BeSubsetOf(samples, "non si inventano valori: ogni campione restituito è reale");
    }

    [Fact]
    public void Peaks_and_drops_survive_the_reduction()
    {
        var samples = Series(10_000, i => i switch { 4321 => 201, 7777 => 41, _ => 140 });

        var result = SeriesDownsampler.Downsample(samples, 200);

        result.Max(s => s.Value).Should().Be(201);
        result.Min(s => s.Value).Should().Be(41);
    }

    [Fact]
    public void Limit_below_two_is_rejected()
    {
        var act = () => SeriesDownsampler.Downsample(Series(5, i => i), 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
