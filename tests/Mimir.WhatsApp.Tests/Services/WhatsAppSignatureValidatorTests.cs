namespace Mimir.WhatsApp.Tests.Services;

using Mimir.WhatsApp.Services;
using Shouldly;

public sealed class WhatsAppSignatureValidatorTests
{
    [Fact]
    public void IsValid_CorrectSignature_ReturnsTrue()
    {
        // Arrange
        var payload = """{"object":"whatsapp_business_account"}""";
        var appSecret = "test-secret";
        var expectedSignature = ComputeExpectedSignature(payload, appSecret);

        // Act
        var result = WhatsAppSignatureValidator.IsValid(payload, $"sha256={expectedSignature}", appSecret);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_IncorrectSignature_ReturnsFalse()
    {
        var payload = """{"object":"whatsapp_business_account"}""";
        var result = WhatsAppSignatureValidator.IsValid(payload, "sha256=invalid", "test-secret");

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_EmptySignature_ReturnsFalse()
    {
        var result = WhatsAppSignatureValidator.IsValid("payload", string.Empty, "secret");
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_EmptyAppSecret_ReturnsFalse()
    {
        var result = WhatsAppSignatureValidator.IsValid("payload", "sha256=abc", string.Empty);
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_SignatureWithoutPrefix_ReturnsTrue()
    {
        var payload = """{"test":"data"}""";
        var appSecret = "my-secret";
        var expectedSignature = ComputeExpectedSignature(payload, appSecret);

        var result = WhatsAppSignatureValidator.IsValid(payload, expectedSignature, appSecret);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_CaseInsensitivePrefix_ReturnsTrue()
    {
        var payload = """{"test":"data"}""";
        var appSecret = "my-secret";
        var expectedSignature = ComputeExpectedSignature(payload, appSecret);

        var result = WhatsAppSignatureValidator.IsValid(payload, $"SHA256={expectedSignature}", appSecret);

        result.ShouldBeTrue();
    }

    private static string ComputeExpectedSignature(string payload, string secret)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hash);
    }
}
