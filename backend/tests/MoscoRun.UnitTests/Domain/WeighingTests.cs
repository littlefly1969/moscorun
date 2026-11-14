using MoscoRun.Domain.Measurements;

namespace MoscoRun.UnitTests.Domain;

public sealed class WeighingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    private static WeighingReading Reading(
        string id = "scale-1:2026-11-12T07:00:00Z", decimal kg = 78.456m, int? ohm = 480, DateTimeOffset? at = null) =>
        new(id, at ?? Now.AddMinutes(-1), kg, ohm);

    [Fact]
    public void Create_valid_reading_normalizes_weight_and_time()
    {
        var at = new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.FromHours(1));
        var result = Weighing.Create(Owner, DeviceId, Reading(at: at), Now);

        result.IsSuccess.Should().BeTrue();
        var w = result.Value;
        w.WeightKg.Should().Be(78.46m);
        w.MeasuredAt.Offset.Should().Be(TimeSpan.Zero);
        w.MeasuredAt.Should().Be(at);
        w.Attribution.Should().Be(AttributionStatus.Assumed);
        w.ReceivedAt.Should().Be(Now);
        w.Id.Version.Should().Be(7);
    }

    [Theory]
    [InlineData(1.99)]
    [InlineData(300.01)]
    [InlineData(0)]
    [InlineData(-70)]
    public void Create_rejects_weight_out_of_range(double kg)
    {
        var result = Weighing.Create(Owner, DeviceId, Reading(kg: (decimal)kg), Now);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "weighing.weight.out_of_range" && e.Field == "weightKg");
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(300.0)]
    public void Create_accepts_weight_bounds(double kg) =>
        Weighing.Create(Owner, DeviceId, Reading(kg: (decimal)kg), Now).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("con spazi")]
    [InlineData("emoji-🏃")]
    [InlineData("a/b")]
    public void Create_rejects_invalid_source_event_id(string id) =>
        Weighing.Create(Owner, DeviceId, Reading(id: id), Now).Errors
            .Should().ContainSingle(e => e.Code == "weighing.source_event_id.invalid");

    [Fact]
    public void Create_rejects_too_long_source_event_id() =>
        Weighing.Create(Owner, DeviceId, Reading(id: new string('a', Weighing.SourceEventIdMaxLength + 1)), Now)
            .IsSuccess.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(3001)]
    public void Create_rejects_impedance_out_of_range(int ohm) =>
        Weighing.Create(Owner, DeviceId, Reading(ohm: ohm), Now).Errors
            .Should().ContainSingle(e => e.Field == "impedanceOhm");

    [Fact]
    public void Create_keeps_missing_impedance_missing() =>
        Weighing.Create(Owner, DeviceId, Reading(ohm: null), Now).Value.ImpedanceOhm.Should().BeNull();

    [Fact]
    public void Create_tolerates_small_clock_skew_but_rejects_future()
    {
        Weighing.Create(Owner, DeviceId, Reading(at: Now + Weighing.MaxClockSkew), Now).IsSuccess.Should().BeTrue();
        Weighing.Create(Owner, DeviceId, Reading(at: Now + Weighing.MaxClockSkew + TimeSpan.FromSeconds(1)), Now)
            .Errors.Should().ContainSingle(e => e.Field == "measuredAt");
    }

    [Fact]
    public void Create_reports_all_errors_together()
    {
        var result = Weighing.Create(Owner, DeviceId, new WeighingReading("", Now.AddDays(1), 0m, 0), Now);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["sourceEventId", "weightKg", "impedanceOhm", "measuredAt"]);
    }

    [Fact]
    public void Value_of_failed_result_throws()
    {
        var result = Weighing.Create(Owner, DeviceId, Reading(kg: 0m), Now);
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsSameMeasurementAs_detects_replay_and_conflict()
    {
        var reading = Reading();
        var w = Weighing.Create(Owner, DeviceId, reading, Now).Value;

        w.IsSameMeasurementAs(reading).Should().BeTrue();
        w.IsSameMeasurementAs(reading with { WeightKg = 78.4561m }).Should().BeTrue("il peso viene arrotondato a 10 g");
        w.IsSameMeasurementAs(reading with { MeasuredAt = reading.MeasuredAt.ToOffset(TimeSpan.FromHours(2)) }).Should().BeTrue();
        w.IsSameMeasurementAs(reading with { WeightKg = 79m }).Should().BeFalse();
        w.IsSameMeasurementAs(reading with { ImpedanceOhm = null }).Should().BeFalse();
        w.IsSameMeasurementAs(reading with { MeasuredAt = reading.MeasuredAt.AddSeconds(1) }).Should().BeFalse();
    }

    [Fact]
    public void MeasuredAt_is_truncated_to_database_precision_so_replays_match()
    {
        var precise = new DateTimeOffset(2026, 11, 12, 6, 59, 0, TimeSpan.Zero).AddTicks(1_234_567);
        var reading = Reading(at: precise);
        var w = Weighing.Create(Owner, DeviceId, reading, Now).Value;

        w.MeasuredAt.Ticks.Should().Be(precise.Ticks - 7);
        w.IsSameMeasurementAs(reading).Should().BeTrue();
    }

    [Fact]
    public void SetAttribution_rejects_unknown_values()
    {
        var w = Weighing.Create(Owner, DeviceId, Reading(), Now).Value;
        w.SetAttribution(AttributionStatus.Rejected);
        w.Attribution.Should().Be(AttributionStatus.Rejected);

        var act = () => w.SetAttribution((AttributionStatus)42);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
