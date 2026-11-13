using MoscoRun.Domain.Devices;

namespace MoscoRun.UnitTests.Domain;

public sealed class DeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 7, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Hash = new byte[32];

    [Fact]
    public void Register_trims_name()
    {
        var id = Guid.NewGuid();
        var d = Device.Register(id, Guid.NewGuid(), "  Bilancia bagno ", DeviceKind.ScaleAgent, Hash, Now).Value;
        d.Id.Should().Be(id);
        d.Name.Should().Be("Bilancia bagno");
        d.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_empty_name(string name) =>
        Device.Register(Guid.NewGuid(), Guid.NewGuid(), name, DeviceKind.ScaleAgent, Hash, Now)
            .Errors.Should().ContainSingle(e => e.Field == "name");

    [Fact]
    public void Register_rejects_long_name_and_unknown_kind()
    {
        var result = Device.Register(Guid.NewGuid(), Guid.NewGuid(), new string('x', 81), (DeviceKind)9, Hash, Now);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["name", "kind"]);
    }

    [Fact]
    public void Register_requires_sha256_hash_and_id()
    {
        var badHash = () => Device.Register(Guid.NewGuid(), Guid.NewGuid(), "x", DeviceKind.PhoneBridge, new byte[16], Now);
        badHash.Should().Throw<ArgumentException>();
        var emptyId = () => Device.Register(Guid.Empty, Guid.NewGuid(), "x", DeviceKind.PhoneBridge, Hash, Now);
        emptyId.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Revoke_is_idempotent_and_keeps_first_time()
    {
        var d = Device.Register(Guid.NewGuid(), Guid.NewGuid(), "x", DeviceKind.ScaleAgent, Hash, Now).Value;
        d.Revoke(Now);
        d.Revoke(Now.AddHours(1));
        d.RevokedAt.Should().Be(Now);
        d.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Touch_writes_at_most_once_per_minute()
    {
        var d = Device.Register(Guid.NewGuid(), Guid.NewGuid(), "x", DeviceKind.ScaleAgent, Hash, Now).Value;
        d.Touch(Now).Should().BeTrue();
        d.Touch(Now.AddSeconds(59)).Should().BeFalse();
        d.Touch(Now.AddMinutes(1)).Should().BeTrue();
        d.LastSeenAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Ingestion_is_tracked_separately_from_contact_and_never_goes_back()
    {
        var d = Device.Register(Guid.NewGuid(), Guid.NewGuid(), "x", DeviceKind.ScaleAgent, Hash, Now).Value;
        d.Touch(Now);
        d.LastIngestedAt.Should().BeNull("un contatto non è un dato ricevuto");

        d.RecordIngestion(Now.AddMinutes(5));
        d.RecordIngestion(Now.AddMinutes(2));
        d.LastIngestedAt.Should().Be(Now.AddMinutes(5));
        d.LastSeenAt.Should().Be(Now);
    }
}
