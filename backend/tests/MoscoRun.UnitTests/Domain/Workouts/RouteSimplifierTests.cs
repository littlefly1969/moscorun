using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

public sealed class RouteSimplifierTests
{
    private static TrackPoint P(int second, double lat, double lon) => new(TestTracks.Start.AddSeconds(second), lat, lon);

    [Fact]
    public void Short_track_is_returned_unchanged()
    {
        var points = TestTracks.Run(10);

        RouteSimplifier.Simplify(points, 10).Should().BeSameAs(points);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(500)]
    [InlineData(3599)]
    public void Result_has_exactly_the_requested_points_in_original_order_with_endpoints(int max)
    {
        var points = TestTracks.Run(3600);

        var result = RouteSimplifier.Simplify(points, max);

        result.Should().HaveCount(max);
        result[0].Should().Be(points[0]);
        result[^1].Should().Be(points[^1]);
        result.Select(p => p.Time).Should().BeInAscendingOrder();
        result.Should().OnlyContain(p => points.Contains(p));
    }

    [Fact]
    public void Corner_of_an_L_shaped_route_is_kept_before_collinear_points()
    {
        TrackPoint[] points =
        [
            P(0, 41.0, 12.0), P(1, 41.0, 12.001), P(2, 41.0, 12.002), P(3, 41.0, 12.003),
            P(4, 41.001, 12.003), P(5, 41.002, 12.003), P(6, 41.003, 12.003),
        ];

        RouteSimplifier.Simplify(points, 3).Should().Equal(points[0], points[3], points[6]);
    }

    [Fact]
    public void Simplified_shape_stays_within_a_few_metres_of_the_original()
    {
        var points = TestTracks.Run(3600);

        var result = RouteSimplifier.Simplify(points, 1000);

        // Ogni punto originale dista poco dalla polilinea semplificata (stessa proiezione locale).
        var kx = Math.Cos(points[0].Latitude * Math.PI / 180) * 111_195;
        const double ky = 111_195;
        double Distance(TrackPoint p, TrackPoint a, TrackPoint b)
        {
            double px = p.Longitude * kx, py = p.Latitude * ky, ax = a.Longitude * kx, ay = a.Latitude * ky;
            double dx = (b.Longitude * kx) - ax, dy = (b.Latitude * ky) - ay;
            var l = (dx * dx) + (dy * dy);
            var t = l == 0 ? 0 : Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / l, 0, 1);
            return Math.Sqrt(Math.Pow(ax + (t * dx) - px, 2) + Math.Pow(ay + (t * dy) - py, 2));
        }

        var segment = 0;
        foreach (var p in points)
        {
            while (segment < result.Count - 2 && p.Time > result[segment + 1].Time)
            {
                segment++;
            }

            Distance(p, result[segment], result[segment + 1]).Should().BeLessThan(2.0);
        }
    }

    [Fact]
    public void Identical_points_do_not_break_the_ranking()
    {
        var points = Enumerable.Range(0, 50).Select(i => P(i, 41.0, 12.0)).ToArray();

        RouteSimplifier.Simplify(points, 5).Should().HaveCount(5);
    }

    [Fact]
    public void Requesting_fewer_than_two_points_is_a_programming_error()
    {
        var act = () => RouteSimplifier.Simplify(TestTracks.Run(10), 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
