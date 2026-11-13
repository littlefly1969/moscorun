using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MoscoRun.Api.Commands;

/// <summary>
/// Genera il certificato che cifra a riposo le chiavi Data Protection salvate nel database.
/// Stampa solo il PFX in Base64: lo script di installazione lo salva in <c>.env</c> (permessi 600), mai nel repository.
/// </summary>
internal static class DataProtectionCertificateCommand
{
    public static int Run(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var certificate = Create(DateTimeOffset.UtcNow);
        output.WriteLine(Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12)));
        return 0;
    }

    public static X509Certificate2 Create(DateTimeOffset now)
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=MoscoRun Data Protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, critical: true));
        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(20));
    }
}
