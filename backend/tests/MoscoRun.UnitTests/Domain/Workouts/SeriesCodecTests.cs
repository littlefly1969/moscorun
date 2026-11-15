using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

public sealed class SeriesCodecTests
{
    [Fact]
    public void Round_trip_returns_normalized_samples()
    {
        var samples = TestTracks.HeartRate(3600);

        var decoded = SeriesCodec.Decode(SeriesCodec.Encode(samples));

        decoded.Should().Equal(samples.Select(SeriesCodec.Normalize));
        decoded.Zip(samples).Should().OnlyContain(p => Math.Abs(p.First.Value - p.Second.Value) <= 0.0005);
    }

    [Fact]
    public void Samples_with_same_time_and_negative_deltas_are_preserved()
    {
        SeriesSample[] samples =
        [
            new(TestTracks.Start, 3.5),
            new(TestTracks.Start, 0),
            new(TestTracks.Start.AddMilliseconds(1), 2999.999),
        ];

        SeriesCodec.Decode(SeriesCodec.Encode(samples)).Should().Equal(samples);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 9 })]
    [InlineData(new byte[] { 1, 5, 0 })]
    [InlineData(new byte[] { 1, 1, 2 })]
    [InlineData(new byte[] { 1, 0, 1 })]
    public void Corrupted_data_is_rejected(byte[] data)
    {
        var act = () => SeriesCodec.Decode(data);

        act.Should().Throw<InvalidDataException>();
    }
}
