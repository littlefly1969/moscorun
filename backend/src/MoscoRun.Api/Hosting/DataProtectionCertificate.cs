using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MoscoRun.Api.Hosting;

/// <summary>Certificato (PFX in Base64, da <c>DataProtection:CertificateBase64</c>) che cifra le chiavi dei cookie a riposo.</summary>
internal static class DataProtectionCertificate
{
    public const string ConfigurationKey = "DataProtection:CertificateBase64";

    public static bool TryLoad(string? base64, out X509Certificate2? certificate, out string? error)
    {
        certificate = null;
        error = null;
        if (string.IsNullOrWhiteSpace(base64))
        {
            error = "assente";
            return false;
        }

        try
        {
            var loaded = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64.Trim()), password: null, X509KeyStorageFlags.EphemeralKeySet);
            if (!loaded.HasPrivateKey || loaded.GetRSAPrivateKey() is null)
            {
                loaded.Dispose();
                error = "il certificato non contiene una chiave privata RSA";
                return false;
            }

            certificate = loaded;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            error = "non è un PFX Base64 valido";
            return false;
        }
    }
}
