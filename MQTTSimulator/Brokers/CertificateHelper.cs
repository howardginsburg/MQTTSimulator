using System.Security.Cryptography.X509Certificates;

namespace MQTTSimulator.Brokers;

public static class CertificateHelper
{
    public static X509Certificate2 LoadFromPem(string path)
    {
        var pem = File.ReadAllText(path);
        var base64 = pem
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\r", "")
            .Replace("\n", "");
        return new X509Certificate2(Convert.FromBase64String(base64));
    }

    public static X509Certificate2 LoadClientCertFromPem(string certPath, string keyPath)
    {
        using var ephemeral = X509Certificate2.CreateFromPemFile(certPath, keyPath);
        return new X509Certificate2(ephemeral.Export(X509ContentType.Pfx));
    }
}
