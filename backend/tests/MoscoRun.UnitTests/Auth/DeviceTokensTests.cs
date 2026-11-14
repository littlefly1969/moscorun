using MoscoRun.Api.Auth;

namespace MoscoRun.UnitTests.Auth;

public sealed class DeviceTokensTests
{
    [Fact]
    public void Created_token_round_trips_and_matches_hash()
    {
        var id = Guid.CreateVersion7();
        var (token, hash) = DeviceTokens.Create(id);

        token.Should().StartWith("mr1_");
        DeviceTokens.TryParse(token, out var parsedId, out var secret).Should().BeTrue();
        parsedId.Should().Be(id);
        DeviceTokens.Matches(secret!, hash).Should().BeTrue();
        hash.Should().HaveCount(32);
    }

    [Fact]
    public void Tokens_are_unique()
    {
        var id = Guid.NewGuid();
        DeviceTokens.Create(id).Token.Should().NotBe(DeviceTokens.Create(id).Token);
    }

    [Fact]
    public void Wrong_secret_does_not_match()
    {
        var (_, hash) = DeviceTokens.Create(Guid.NewGuid());
        var (other, _) = DeviceTokens.Create(Guid.NewGuid());
        DeviceTokens.TryParse(other, out _, out var secret).Should().BeTrue();
        DeviceTokens.Matches(secret!, hash).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer abc")]
    [InlineData("mr1_")]
    [InlineData("mr1_nothex_secret")]
    [InlineData("mr2_0123456789abcdef0123456789abcdef_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("mr1_0123456789abcdef0123456789abcdef_short")]
    [InlineData("mr1_0123456789abcdef0123456789abcdef_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA!")]
    public void Malformed_tokens_are_rejected(string? token) =>
        DeviceTokens.TryParse(token, out _, out _).Should().BeFalse();
}
