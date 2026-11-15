using MoscoRun.Api.Features.Maps;

namespace MoscoRun.UnitTests;

public sealed class MapsOptionsTests
{
    // Chiave finta di forma valida, spezzata in due: nessun letterale simile a una chiave nel repository (gitleaks).
    private const string Key = "AIza" + "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";

    [Theory]
    [InlineData(null, null, "none")]
    [InlineData(null, Key, "google")]
    [InlineData("none", Key, "none")]
    [InlineData(" Google ", Key, "google")]
    public void Provider_defaults_to_google_only_when_a_key_is_present(string? provider, string? key, string expected)
    {
        new MapsOptions { Provider = provider, GoogleApiKey = key }.EffectiveProvider.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, Key, true)]
    [InlineData("google", null, false)]
    [InlineData("google", "AIza-troppo-corta", false)]
    [InlineData("openstreetmap", null, false)]
    public void Validation_requires_a_well_formed_key_for_google(string? provider, string? key, bool valid)
    {
        var result = new MapsOptionsValidation().Validate(null, new MapsOptions { Provider = provider, GoogleApiKey = key });

        result.Succeeded.Should().Be(valid);
    }
}
