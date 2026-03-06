namespace Mimir.WhatsApp.Tests.Services;

using Mimir.WhatsApp.Models;
using Mimir.WhatsApp.Services;
using nem.Contracts.Content;
using Shouldly;

public sealed class WhatsAppMessageConverterTests
{
    [Fact]
    public void ToContentPayload_TextMessage_ReturnsTextContent()
    {
        // Arrange
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-1",
            Timestamp = "1709740800",
            Type = "text",
            Text = new WhatsAppTextBody { Body = "Hello from WhatsApp" },
        };

        // Act
        var result = WhatsAppMessageConverter.ToContentPayload(message);

        // Assert
        result.ShouldBeOfType<TextContent>();
        var text = (TextContent)result;
        text.Text.ShouldBe("Hello from WhatsApp");
        text.CreatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void ToContentPayload_AudioMessage_ReturnsVoiceContent()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-2",
            Timestamp = "1709740800",
            Type = "audio",
            Audio = new WhatsAppMediaBody { Id = "media-1", MimeType = "audio/ogg" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        result.ShouldBeOfType<VoiceContent>();
        var voice = (VoiceContent)result;
        voice.MimeType.ShouldBe("audio/ogg");
        voice.TranscriptionText.ShouldBeNull();
    }

    [Fact]
    public void ToContentPayload_ImageMessage_ReturnsTextContentWithFormat()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-3",
            Timestamp = "1709740800",
            Type = "image",
            Image = new WhatsAppMediaBody { Id = "media-2", MimeType = "image/jpeg", Caption = "My photo" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        result.ShouldBeOfType<TextContent>();
        var text = (TextContent)result;
        text.Text.ShouldBe("My photo");
        text.Format.ShouldBe("whatsapp-image");
    }

    [Fact]
    public void ToContentPayload_ImageMessage_NoCaption_ReturnsPlaceholder()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-3",
            Type = "image",
            Image = new WhatsAppMediaBody { Id = "media-2", MimeType = "image/jpeg" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        var text = (TextContent)result;
        text.Text.ShouldBe("[Image received]");
    }

    [Fact]
    public void ToContentPayload_VideoMessage_ReturnsTextContentWithFormat()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-4",
            Type = "video",
            Video = new WhatsAppMediaBody { Id = "media-3", MimeType = "video/mp4", Caption = "My video" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        var text = (TextContent)result;
        text.Text.ShouldBe("My video");
        text.Format.ShouldBe("whatsapp-video");
    }

    [Fact]
    public void ToContentPayload_DocumentMessage_ReturnsTextContentWithFilename()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-5",
            Type = "document",
            Document = new WhatsAppDocumentBody { Id = "media-4", Filename = "report.pdf" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        var text = (TextContent)result;
        text.Text.ShouldBe("[Document: report.pdf]");
        text.Format.ShouldBe("whatsapp-document");
    }

    [Fact]
    public void ToContentPayload_UnsupportedType_ReturnsPlaceholder()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-6",
            Type = "sticker",
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        var text = (TextContent)result;
        text.Text.ShouldBe("[Unsupported message type: sticker]");
    }

    [Fact]
    public void ToContentPayload_TextMessage_NullTimestamp_CreatedAtIsNull()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-7",
            Type = "text",
            Text = new WhatsAppTextBody { Body = "test" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        result.CreatedAt.ShouldBeNull();
    }

    [Fact]
    public void ToContentPayload_TextMessage_InvalidTimestamp_CreatedAtIsNull()
    {
        var message = new WhatsAppMessage
        {
            From = "1234567890",
            Id = "msg-8",
            Timestamp = "not-a-number",
            Type = "text",
            Text = new WhatsAppTextBody { Body = "test" },
        };

        var result = WhatsAppMessageConverter.ToContentPayload(message);

        result.CreatedAt.ShouldBeNull();
    }
}
