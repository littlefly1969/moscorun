using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

public sealed class RoutePrivacyTests
{
    // Casa a Roma; un grado di latitudine vale circa 111 km.
    private static readonly PrivacyZone Home = new(41.9000, 12.5000, 200);

    private static TrackPoint P(int s, double lat, double lon) => new(TestTracks.Start.AddSeconds(s), lat, lon);

    [Fact]
    public void Start_and_end_inside_a_zone_are_hidden_but_middle_passages_stay()
    {
        TrackPoint[] points =
        [
            P(0, 41.9000, 12.5000), P(1, 41.9010, 12.5000), // dentro (0 e ~111 m)
            P(2, 41.9050, 12.5000), P(3, 41.9001, 12.5001), P(4, 41.9100, 12.5000), // passaggio interno a metà
            P(5, 41.9005, 12.5000), P(6, 41.9000, 12.5000), // ritorno a casa
        ];

        var visible = RoutePrivacy.Apply(points, [Home]);

        visible.Points.Should().Equal(points[2], points[3], points[4]);
        visible.HiddenStart.Should().Be(2);
        visible.HiddenEnd.Should().Be(2);
    }

    [Fact]
    public void Without_zones_the_route_is_unchanged()
    {
        var points = TestTracks.Run(100);

        var visible = RoutePrivacy.Apply(points, []);

        visible.Points.Should().BeSameAs(points);
        (visible.HiddenStart, visible.HiddenEnd).Should().Be((0, 0));
    }

    [Fact]
    public void Route_entirely_inside_a_zone_is_completely_hidden()
    {
        TrackPoint[] points = [P(0, 41.9, 12.5), P(1, 41.9001, 12.5), P(2, 41.9002, 12.5)];

        var visible = RoutePrivacy.Apply(points, [Home]);

        visible.Points.Should().BeEmpty();
        visible.HiddenStart.Should().Be(3);
        visible.HiddenEnd.Should().Be(0);
    }

    [Theory]
    [InlineData(41.9017, 12.5, true)] // ~189 m
    [InlineData(41.9019, 12.5, false)] // ~211 m
    [InlineData(41.9, 12.5024, true)] // ~199 m in longitudine (cos 41.9°)
    [InlineData(41.9, 12.5025, false)]
    public void Contains_uses_great_circle_distance(double lat, double lon, bool inside)
    {
        Home.Contains(new TrackPoint(TestTracks.Start, lat, lon)).Should().Be(inside);
    }

    [Fact]
    public void Configuration_string_is_parsed_with_invariant_culture()
    {
        var result = PrivacyZone.ParseList(" 41.9,12.5,200 ; 45.4642,9.19,500;");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(new PrivacyZone(41.9, 12.5, 200), new PrivacyZone(45.4642, 9.19, 500));
        PrivacyZone.ParseList(null).Value.Should().BeEmpty();
        PrivacyZone.ParseList("  ").Value.Should().BeEmpty();
    }

    [Theory]
    [InlineData("41,9;12,5;200")]
    [InlineData("41.9,12.5")]
    [InlineData("91,12.5,200")]
    [InlineData("41.9,181,200")]
    [InlineData("41.9,12.5,10")]
    [InlineData("41.9,12.5,99999")]
    [InlineData("casa")]
    public void Invalid_configuration_is_rejected(string value)
    {
        var result = PrivacyZone.ParseList(value);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Single().Code.Should().Be("privacy.zone.invalid");
    }
}
