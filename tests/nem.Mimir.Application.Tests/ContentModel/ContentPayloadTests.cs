using System.Text.Json;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Contracts.Content;
using nem.Contracts.Versioning;
using Shouldly;

namespace nem.Mimir.Application.Tests.ContentModel;

public sealed class ContentPayloadTests
{
    private static readonly JsonSerializerOptions JsonOptions = NemJsonSerializerOptions.CreateOptions();

    [Fact]
    public void TextContent_SerializationRoundTrip_PreservesAllFields()
    {
        var original = new TextContent("Hello world", "markdown")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize<IContentPayload>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IContentPayload>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<TextContent>();
        var text = (TextContent)deserialized;
        text.Text.ShouldBe("Hello world");
        text.Format.ShouldBe("markdown");
        text.ContentType.ShouldBe("text");
        text.CreatedAt.ShouldBe(original.CreatedAt);
    }

    [Fact]
    public void CanvasContent_SerializationRoundTrip_PreservesAllFields()
    {
        var original = new CanvasContent("My Canvas")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize<IContentPayload>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IContentPayload>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<CanvasContent>();
        var canvas = (CanvasContent)deserialized;
        canvas.Title.ShouldBe("My Canvas");
        canvas.ContentType.ShouldBe("canvas");
        canvas.CreatedAt.ShouldBe(original.CreatedAt);
    }

    [Fact]
    public void VoiceContent_SerializationRoundTrip_PreservesAllFields()
    {
        var original = new VoiceContent("Hello there", "https://audio.example.com/clip.ogg", 3.5, "audio/ogg")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize<IContentPayload>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IContentPayload>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<VoiceContent>();
        var voice = (VoiceContent)deserialized;
        voice.TranscriptionText.ShouldBe("Hello there");
        voice.AudioUrl.ShouldBe("https://audio.example.com/clip.ogg");
        voice.DurationSeconds.ShouldBe(3.5);
        voice.MimeType.ShouldBe("audio/ogg");
        voice.ContentType.ShouldBe("voice");
    }

    [Fact]
    public void ResolvedPayload_ReturnsContentPayload_WhenSet()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "original text");

        var payload = new TextContent("rich text", "html");
        message.SetContentPayload(payload);

        message.ResolvedPayload.ShouldBe(payload);
        message.ContentType.ShouldBe("text");
    }

    [Fact]
    public void ResolvedPayload_WrapsContentString_WhenContentPayloadIsNull()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "plain text message");

        message.ContentPayload.ShouldBeNull();

        var resolved = message.ResolvedPayload;
        resolved.ShouldBeOfType<TextContent>();
        ((TextContent)resolved).Text.ShouldBe("plain text message");
    }

    [Fact]
    public void UnknownFields_CapturedByExtensionData_TolerantDeserialization()
    {
        var json = """
            {
                "contentType": "text",
                "text": "hello",
                "format": null,
                "futureField": "unknown-value",
                "futureNumber": 42
            }
            """;

        var deserialized = JsonSerializer.Deserialize<IContentPayload>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<TextContent>();
        var text = (TextContent)deserialized;
        text.Text.ShouldBe("hello");
        text.ExtensionData.ShouldNotBeNull();
        text.ExtensionData!.ShouldContainKey("futureField");
        text.ExtensionData!.ShouldContainKey("futureNumber");
    }

    [Fact]
    public void NemJsonSerializerOptions_HandlesIContentPayload_Correctly()
    {
        var options = NemJsonSerializerOptions.Default;

        var payload = new TextContent("test", "plain")
        {
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize<IContentPayload>(payload, options);

        json.ShouldContain("\"contentType\":\"text\"");
        json.ShouldContain("\"text\":\"test\"");
        json.ShouldContain("\"format\":\"plain\"");

        var deserialized = JsonSerializer.Deserialize<IContentPayload>(json, options);
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<TextContent>();
    }

    [Fact]
    public void ContentType_Discriminator_AppearsInSerializedJson()
    {
        IContentPayload[] payloads =
        [
            new TextContent("t"),
            new CanvasContent("c"),
            new VoiceContent(null, null, null, null),
        ];

        var expectedTypes = new[] { "text", "canvas", "voice" };

        for (var i = 0; i < payloads.Length; i++)
        {
            var json = JsonSerializer.Serialize<IContentPayload>(payloads[i], JsonOptions);
            json.ShouldContain($"\"contentType\":\"{expectedTypes[i]}\"");
        }
    }

    [Fact]
    public void SetContentPayload_SetsContentTypeColumn()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.Assistant, "response");

        message.SetContentPayload(new CanvasContent("Canvas Title"));

        message.ContentType.ShouldBe("canvas");
        message.ContentPayload.ShouldBeOfType<CanvasContent>();
    }

    [Fact]
    public void SetContentPayload_ThrowsOnNull()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "text");

        Should.Throw<ArgumentNullException>(() => message.SetContentPayload(null!));
    }

    [Fact]
    public void ExistingContentProperty_RemainsUnchanged_WhenPayloadSet()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "original content");

        message.SetContentPayload(new VoiceContent("transcribed", null, 5.0, "audio/mp3"));

        message.Content.ShouldBe("original content");
        message.ContentPayload.ShouldBeOfType<VoiceContent>();
    }
}
