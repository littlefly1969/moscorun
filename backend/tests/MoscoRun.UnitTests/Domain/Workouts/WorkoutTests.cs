using MoscoRun.Domain.Workouts;

namespace MoscoRun.UnitTests.Domain.Workouts;

public sealed class WorkoutTests
{
    private static readonly DateTimeOffset Now = TestTracks.Start.AddHours(3);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    internal static WorkoutRecording Recording(
        long revision = 1,
        TrackPoint[]? track = null,
        double? distance = 10_012.346,
        DateTimeOffset? end = null,
        string sourceId = "uid-1:0",
        IReadOnlyDictionary<SeriesMetric, IReadOnlyList<SeriesSample>>? series = null) =>
        new(
            "samsung-health",
            sourceId,
            revision,
            "RUNNING",
            TestTracks.Start,
            end ?? TestTracks.Start.AddHours(1),
            60,
            TimeSpan.FromMinutes(58.5),
            distance,
            712.4,
            141.27,
            176,
            track ?? TestTracks.Run(3600),
            series ?? new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.HeartRate] = TestTracks.HeartRate(3600) });

    private static WorkoutContent Valid(WorkoutRecording? recording = null) =>
        Workout.Create(Owner, DeviceId, recording ?? Recording(), Now).Value;

    [Fact]
    public void Create_builds_summary_route_and_series()
    {
        var content = Valid();
        var w = content.Workout;

        w.Id.Version.Should().Be(7);
        w.TrackPointCount.Should().Be(3600);
        w.HasTrack.Should().BeTrue();
        w.Bounds.Should().NotBeNull();
        w.DistanceM.Should().Be(10_012.35);
        w.ContentHash.Should().HaveCount(32);
        content.Route!.WorkoutId.Should().Be(w.Id);
        content.Route.Points().Should().HaveCount(3600);
        content.Series.Should().ContainSingle().Which.Metric.Should().Be(SeriesMetric.HeartRate);
    }

    [Fact]
    public void Points_are_sorted_by_time_and_bounds_cover_them()
    {
        var track = TestTracks.Run(600);
        var shuffled = track.OrderBy(_ => Random.Shared.Next()).ToArray();

        var content = Valid(Recording(track: shuffled));

        content.Route!.Points().Should().Equal(track.Select(TrackCodec.Normalize));
        var b = content.Workout.Bounds!;
        track.Should().OnlyContain(p => p.Latitude >= b.MinLatitude - 1e-7 && p.Latitude <= b.MaxLatitude + 1e-7
            && p.Longitude >= b.MinLongitude - 1e-7 && p.Longitude <= b.MaxLongitude + 1e-7);
    }

    [Fact]
    public void Workout_without_gps_has_no_route()
    {
        var content = Valid(Recording(track: []));

        content.Route.Should().BeNull();
        content.Workout.HasTrack.Should().BeFalse();
        content.Workout.Bounds.Should().BeNull();
    }

    [Fact]
    public void Same_content_has_same_hash_regardless_of_point_order_or_revision()
    {
        var a = Valid(Recording(revision: 1));
        var b = Valid(Recording(revision: 5, track: [.. TestTracks.Run(3600).Reverse()]));

        a.Workout.ContentHash.Should().Equal(b.Workout.ContentHash);
        a.Workout.CompareWith(b).Should().Be(WorkoutReplay.Identical);
    }

    [Fact]
    public void Changed_content_is_newer_only_with_a_higher_revision()
    {
        var stored = Valid(Recording(revision: 2)).Workout;

        stored.CompareWith(Valid(Recording(revision: 3, distance: 9000))).Should().Be(WorkoutReplay.Newer);
        stored.CompareWith(Valid(Recording(revision: 2, distance: 9000))).Should().Be(WorkoutReplay.Conflict);
        stored.CompareWith(Valid(Recording(revision: 1, distance: 9000))).Should().Be(WorkoutReplay.Conflict);
    }

    [Fact]
    public void Replace_keeps_identity_and_rebuilds_children()
    {
        var stored = Valid(Recording(revision: 1)).Workout;
        var newer = Valid(Recording(revision: 2, track: TestTracks.Run(100)));

        var replaced = stored.ReplaceWith(newer, Now.AddMinutes(1));

        replaced.Workout.Should().BeSameAs(stored);
        stored.Revision.Should().Be(2);
        stored.TrackPointCount.Should().Be(100);
        stored.UpdatedAt.Should().Be(Now.AddMinutes(1), "Now è già al microsecondo");
        replaced.Route!.WorkoutId.Should().Be(stored.Id);
        replaced.Series.Should().OnlyContain(s => s.WorkoutId == stored.Id);
        stored.ContentHash.Should().Equal(newer.Workout.ContentHash);
    }

    [Fact]
    public void Replace_with_older_revision_is_a_programming_error()
    {
        var stored = Valid(Recording(revision: 3)).Workout;

        var act = () => stored.ReplaceWith(Valid(Recording(revision: 3, distance: 1)), Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(InvalidRecordings))]
    public void Invalid_recordings_are_rejected_with_a_stable_code(WorkoutRecording recording, string code)
    {
        var result = Workout.Create(Owner, DeviceId, recording, Now);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Select(e => e.Code).Should().Contain(code);
    }

    public static TheoryData<WorkoutRecording, string> InvalidRecordings()
    {
        var ok = Recording(track: TestTracks.Run(10));
        var bad = new TrackPoint(TestTracks.Start, 91, 12);
        var late = new TrackPoint(TestTracks.Start.AddHours(2), 41, 12);
        return new TheoryData<WorkoutRecording, string>
        {
            { ok with { Source = "Samsung Health" }, "workout.source.invalid" },
            { ok with { SourceId = "" }, "workout.source_id.invalid" },
            { ok with { SourceId = new string('a', 129) }, "workout.source_id.invalid" },
            { ok with { ActivityType = "corsa veloce" }, "workout.activity_type.invalid" },
            { ok with { Revision = -1 }, "workout.revision.invalid" },
            { ok with { EndedAt = TestTracks.Start }, "workout.time.invalid" },
            { ok with { EndedAt = TestTracks.Start.AddDays(8) }, "workout.time.invalid" },
            { ok with { StartedAt = new DateTimeOffset(1999, 12, 31, 0, 0, 0, TimeSpan.Zero), EndedAt = new DateTimeOffset(1999, 12, 31, 1, 0, 0, TimeSpan.Zero), Track = [] }, "workout.time.too_old" },
            { ok with { EndedAt = Now.AddMinutes(10) }, "workout.time.in_future" },
            { ok with { UtcOffsetMinutes = 900 }, "workout.utc_offset.invalid" },
            { ok with { ActiveDuration = TimeSpan.FromHours(2) }, "workout.active_duration.invalid" },
            { ok with { DistanceM = -1 }, "workout.distance.out_of_range" },
            { ok with { DistanceM = double.NaN }, "workout.distance.out_of_range" },
            { ok with { EnergyKcal = 60_000 }, "workout.energy.out_of_range" },
            { ok with { MaxHeartRate = 300 }, "workout.heart_rate.out_of_range" },
            { ok with { Track = [bad] }, "workout.track.point_invalid" },
            { ok with { Track = [late] }, "workout.track.point_invalid" },
            { ok with { Track = [new TrackPoint(TestTracks.Start, 41, 12, AccuracyM: -1)] }, "workout.track.point_invalid" },
            { ok with { Track = new TrackPoint[Workout.MaxTrackPoints + 1] }, "workout.track.too_long" },
            { ok with { Series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.HeartRate] = [new(TestTracks.Start, 10)] } }, "workout.series.sample_invalid" },
            { ok with { Series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.Speed] = [new(TestTracks.Start, double.PositiveInfinity)] } }, "workout.series.sample_invalid" },
            { ok with { Series = new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [(SeriesMetric)99] = [] } }, "workout.series.metric_invalid" },
        };
    }

    [Fact]
    public void Samples_slightly_outside_the_session_are_accepted()
    {
        var early = new TrackPoint(TestTracks.Start - TimeSpan.FromMinutes(9), 41, 12);

        Workout.Create(Owner, DeviceId, Recording(track: [early]), Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Empty_series_are_not_stored()
    {
        var content = Valid(Recording(series: new Dictionary<SeriesMetric, IReadOnlyList<SeriesSample>> { [SeriesMetric.Power] = [] }));

        content.Series.Should().BeEmpty();
    }
}
