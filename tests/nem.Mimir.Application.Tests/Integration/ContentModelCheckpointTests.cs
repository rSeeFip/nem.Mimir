using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.Persistence;
using nem.Contracts.Content;
using nem.Contracts.Versioning;
using Shouldly;

namespace nem.Mimir.Application.Tests.Integration;

public sealed class ContentModelCheckpointTests : IDisposable
{
    private readonly MimirDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = NemJsonSerializerOptions.CreateOptions();

    public ContentModelCheckpointTests()
    {
        var options = new DbContextOptionsBuilder<MimirDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MimirDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private Message CreatePersistedMessage(IContentPayload payload, string content = "test content")
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Checkpoint Test");
        var message = conversation.AddMessage(MessageRole.User, content);
        message.SetContentPayload(payload);

        _context.Conversations.Add(conversation);
        _context.SaveChanges();

        // Detach so the next query hits the store, not the identity cache
        _context.ChangeTracker.Clear();

        return message;
    }

    private Message ReloadMessage(Guid messageId)
    {
        var loaded = _context.Messages.AsNoTracking().Single(m => m.Id == messageId);
        return loaded;
    }

    // ──────────────────────────────────────────────────────────────────────
    // TextContent roundtrip
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TextContent_PersistAndReload_PreservesPayload()
    {
        var payload = new TextContent("Hello, world!", "markdown")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 10, 0, 0, TimeSpan.Zero),
        };

        var original = CreatePersistedMessage(payload);
        var loaded = ReloadMessage(original.Id);

        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<TextContent>();

        var text = (TextContent)loaded.ContentPayload!;
        text.Text.ShouldBe("Hello, world!");
        text.Format.ShouldBe("markdown");
        text.ContentType.ShouldBe("text");
        text.CreatedAt.ShouldBe(payload.CreatedAt);

        loaded.ContentType.ShouldBe("text");
    }

    // ──────────────────────────────────────────────────────────────────────
    // CanvasContent roundtrip
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CanvasContent_PersistAndReload_PreservesPayload()
    {
        var payload = new CanvasContent("My Canvas")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 11, 0, 0, TimeSpan.Zero),
        };

        var original = CreatePersistedMessage(payload);
        var loaded = ReloadMessage(original.Id);

        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<CanvasContent>();

        var canvas = (CanvasContent)loaded.ContentPayload!;
        canvas.Title.ShouldBe("My Canvas");
        canvas.ContentType.ShouldBe("canvas");
        canvas.CreatedAt.ShouldBe(payload.CreatedAt);

        loaded.ContentType.ShouldBe("canvas");
    }

    [Fact]
    public void CanvasContent_WithBlocks_PersistAndReload_PreservesPayload()
    {
        // Use a simple blocks array — block type definitions are deferred (T17)
        var blocks = new List<object> { new { type = "text", text = "hello" } };
        var payload = new CanvasContent("Canvas With Blocks", blocks)
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 11, 30, 0, TimeSpan.Zero),
        };

        var original = CreatePersistedMessage(payload);
        var loaded = ReloadMessage(original.Id);

        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<CanvasContent>();

        var canvas = (CanvasContent)loaded.ContentPayload!;
        canvas.Title.ShouldBe("Canvas With Blocks");
        canvas.Blocks.ShouldNotBeNull();
        canvas.Blocks!.Count.ShouldBe(1);
        canvas.ContentType.ShouldBe("canvas");
    }

    // ──────────────────────────────────────────────────────────────────────
    // VoiceContent roundtrip
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void VoiceContent_PersistAndReload_PreservesPayload()
    {
        var payload = new VoiceContent(
            "Hello there",
            "https://audio.example.com/clip.ogg",
            3.5,
            "audio/ogg")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero),
        };

        var original = CreatePersistedMessage(payload);
        var loaded = ReloadMessage(original.Id);

        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<VoiceContent>();

        var voice = (VoiceContent)loaded.ContentPayload!;
        voice.TranscriptionText.ShouldBe("Hello there");
        voice.AudioUrl.ShouldBe("https://audio.example.com/clip.ogg");
        voice.DurationSeconds.ShouldBe(3.5);
        voice.MimeType.ShouldBe("audio/ogg");
        voice.ContentType.ShouldBe("voice");
        voice.CreatedAt.ShouldBe(payload.CreatedAt);

        loaded.ContentType.ShouldBe("voice");
    }

    [Fact]
    public void VoiceContent_WithNullOptionalFields_PersistAndReload_PreservesPayload()
    {
        var payload = new VoiceContent(null, null, null, null)
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 12, 30, 0, TimeSpan.Zero),
        };

        var original = CreatePersistedMessage(payload);
        var loaded = ReloadMessage(original.Id);

        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<VoiceContent>();

        var voice = (VoiceContent)loaded.ContentPayload!;
        voice.TranscriptionText.ShouldBeNull();
        voice.AudioUrl.ShouldBeNull();
        voice.DurationSeconds.ShouldBeNull();
        voice.MimeType.ShouldBeNull();
        voice.ContentType.ShouldBe("voice");
    }

    // ──────────────────────────────────────────────────────────────────────
    // JSON discriminator verification
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(TextContent), "text")]
    [InlineData(typeof(CanvasContent), "canvas")]
    [InlineData(typeof(VoiceContent), "voice")]
    public void Serialize_PreservesTypeDiscriminator(Type expectedType, string expectedDiscriminator)
    {
        IContentPayload payload = expectedDiscriminator switch
        {
            "text" => new TextContent("t"),
            "canvas" => new CanvasContent("c"),
            "voice" => new VoiceContent(null, null, null, null),
            _ => throw new ArgumentOutOfRangeException(),
        };

        var json = JsonSerializer.Serialize<IContentPayload>(payload, JsonOptions);
        json.ShouldContain($"\"contentType\":\"{expectedDiscriminator}\"");

        var deserialized = JsonSerializer.Deserialize<IContentPayload>(json, JsonOptions);
        deserialized.ShouldNotBeNull();
        deserialized.GetType().ShouldBe(expectedType);
        deserialized.ContentType.ShouldBe(expectedDiscriminator);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Null ContentPayload roundtrip
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NullContentPayload_PersistAndReload_RemainsNull()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Null Payload Test");
        var message = conversation.AddMessage(MessageRole.User, "plain text only");

        _context.Conversations.Add(conversation);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var loaded = _context.Messages.AsNoTracking().Single(m => m.Id == message.Id);

        loaded.ContentPayload.ShouldBeNull();
        loaded.ContentType.ShouldBeNull();
        loaded.Content.ShouldBe("plain text only");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Content property remains unchanged when payload is set
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ContentProperty_Preserved_WhenContentPayloadIsSet()
    {
        var payload = new VoiceContent("transcribed", null, 5.0, "audio/mp3");
        var original = CreatePersistedMessage(payload, "original content string");
        var loaded = ReloadMessage(original.Id);

        loaded.Content.ShouldBe("original content string");
        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<VoiceContent>();
    }
}
