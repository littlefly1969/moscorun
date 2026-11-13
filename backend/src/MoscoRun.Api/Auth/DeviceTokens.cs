using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace MoscoRun.Api.Auth;

/// <summary>
/// Token dei dispositivi: <c>mr1_&lt;deviceId&gt;_&lt;segreto&gt;</c>. L'id permette la ricerca diretta,
/// il segreto (256 bit) è salvato solo come SHA-256 e confrontato in tempo costante.
/// </summary>
public static class DeviceTokens
{
    public const string Prefix = "mr1_";
    private const int SecretBytes = 32;

    public static (string Token, byte[] Hash) Create(Guid deviceId)
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        return ($"{Prefix}{deviceId:N}_{secret}", Hash(secret));
    }

    public static bool TryParse(string? token, out Guid deviceId, [NotNullWhen(true)] out string? secret)
    {
        deviceId = Guid.Empty;
        secret = null;
        if (token is null || !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = token.AsSpan(Prefix.Length);
        var separator = body.IndexOf('_');
        if (separator != 32 || !Guid.TryParseExact(body[..separator], "N", out deviceId))
        {
            return false;
        }

        var candidate = body[(separator + 1)..];
        if (candidate.Length != Base64Url.GetEncodedLength(SecretBytes) || !Base64Url.IsValid(candidate))
        {
            return false;
        }

        secret = candidate.ToString();
        return true;
    }

    public static byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public static bool Matches(string secret, byte[] expectedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);
}
