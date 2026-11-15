using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

public sealed class TrackCodecTests
{
    [Fact]
    public void Round_trip_returns_normalized_points()
    {
        var points = TestTracks.Run(3600);

        var decoded = TrackCodec.Decode(TrackCodec.Encode(points));

        decoded.Should().Equal(points.Select(TrackCodec.Normalize));
    }

    [Fact]
    public void Normalization_loses_at_most_a_centimetre()
    {
        foreach (var (original, decoded) in TestTracks.Run(500).Zip(TrackCodec.Decode(TrackCodec.Encode(TestTracks.Run(500)))))
        {
            Math.Abs(original.Latitude - decoded.Latitude).Should().BeLessThanOrEqualTo(0.5e-7);
            Math.Abs(original.Longitude - decoded.Longitude).Should().BeLessThanOrEqualTo(0.5e-7);
            (decoded.AltitudeM is null).Should().Be(original.AltitudeM is null);
            if (original.AltitudeM is { } alt)
            {
                Math.Abs(alt - decoded.AltitudeM!.Value).Should().BeLessThanOrEqualTo(0.005);
            }
        }
    }

    [Fact]
    public void Encoding_is_deterministic_and_compact()
    {
        var points = TestTracks.Run(3600);

        var a = TrackCodec.Encode(points);
        var b = TrackCodec.Encode(points.Select(TrackCodec.Normalize).ToArray());

        a.Should().Equal(b);
        // Un'ora a 1 Hz: meno di 15 byte per punto (JSON equivalente: circa 100).
        a.Length.Should().BeLessThan(3600 * 15);
    }

    [Fact]
    public void Extreme_values_and_empty_track_round_trip()
    {
        TrackPoint[] points =
        [
            new(DateTimeOffset.FromUnixTimeMilliseconds(946_684_800_000), -90, -180, -1000, 0),
            new(DateTimeOffset.FromUnixTimeMilliseconds(4_102_444_800_000), 90, 180, 10_000, 100_000),
            new(DateTimeOffset.FromUnixTimeMilliseconds(946_684_800_000), 0, 0),
        ];

        TrackCodec.Decode(TrackCodec.Encode(points)).Should().Equal(points);
        TrackCodec.Decode(TrackCodec.Encode([])).Should().BeEmpty();
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 2, 0 })]
    [InlineData(new byte[] { 1, 200 })]
    [InlineData(new byte[] { 1, 1, 0, 2 })]
    [InlineData(new byte[] { 1, 1, 8, 2, 2, 2 })]
    [InlineData(new byte[] { 1, 0, 0 })]
    [InlineData(new byte[] { 1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F })]
    public void Corrupted_data_is_rejected_without_crashing(byte[] data)
    {
        var act = () => TrackCodec.Decode(data);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Every_truncation_of_valid_data_is_rejected()
    {
        var data = TrackCodec.Encode(TestTracks.Run(20));
        for (var length = 0; length < data.Length; length++)
        {
            var act = () => TrackCodec.Decode(data.AsSpan(0, length));
            act.Should().Throw<InvalidDataException>($"lunghezza {length}");
        }
    }
}
