namespace Mimir.WhatsApp.Services;

using System.Security.Cryptography;
using System.Text;

internal static class WhatsAppSignatureValidator
{
    public static bool IsValid(string payload, string signature, string appSecret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(appSecret))
            return false;

        var signatureValue = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature["sha256=".Length..]
            : signature;

        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var expectedHash = HMACSHA256.HashData(keyBytes, payloadBytes);
        var expectedSignature = Convert.ToHexStringLower(expectedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signatureValue.ToLowerInvariant()));
    }
}
