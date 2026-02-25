using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace MQTTSimulator.Brokers;

public static class SasTokenGenerator
{
    public static string Generate(string hostname, string deviceId, string deviceKey, TimeSpan ttl)
    {
        var resourceUri = $"{hostname}/devices/{deviceId}";
        var encodedResourceUri = HttpUtility.UrlEncode(resourceUri);

        var expiry = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var stringToSign = $"{encodedResourceUri}\n{expiry}";

        using var hmac = new HMACSHA256(Convert.FromBase64String(deviceKey));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        var encodedSignature = HttpUtility.UrlEncode(signature);

        return $"SharedAccessSignature sr={encodedResourceUri}&sig={encodedSignature}&se={expiry}";
    }
}
