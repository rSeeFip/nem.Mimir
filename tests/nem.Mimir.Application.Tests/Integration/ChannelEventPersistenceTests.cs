using Microsoft.EntityFrameworkCore;
using nem.Mimir.Infrastructure.Persistence;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using Shouldly;
using ChannelEvent = nem.Mimir.Domain.Entities.ChannelEvent;

namespace nem.Mimir.Application.Tests.Integration;

public sealed class ChannelEventPersistenceTests : IDisposable
{
    private readonly MimirDbContext _context;

    public ChannelEventPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<MimirDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MimirDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private ChannelEvent CreateAndPersist(
        ChannelType channel,
        string externalChannelId,
        string externalUserId,
        IContentPayload? payload,
        DateTimeOffset receivedAt)
    {
        var entity = ChannelEvent.Create(channel, externalChannelId, externalUserId, payload, receivedAt);
        _context.ChannelEvents.Add(entity);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return entity;
    }

    private ChannelEvent? Reload(Guid id) =>
        _context.ChannelEvents.AsNoTracking().FirstOrDefault(e => e.Id == id);

    [Fact]
    public void Create_PersistAndReload_PreservesScalarProperties()
    {
        var timestamp = new DateTimeOffset(2026, 3, 6, 14, 0, 0, TimeSpan.Zero);
        var original = CreateAndPersist(
            ChannelType.Telegram, "tg-chat-123", "tg-user-456", null, timestamp);

        var loaded = Reload(original.Id);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(original.Id);
        loaded.Channel.ShouldBe(ChannelType.Telegram);
        loaded.ExternalChannelId.ShouldBe("tg-chat-123");
        loaded.ExternalUserId.ShouldBe("tg-user-456");
        loaded.ReceivedAt.ShouldBe(timestamp);
    }

    [Fact]
    public void TextContent_PersistAndReload_PreservesPayload()
    {
        var payload = new TextContent("Hello from Telegram!", "plain")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 14, 30, 0, TimeSpan.Zero),
        };

        var original = CreateAndPersist(
            ChannelType.Telegram, "tg-chat-1", "tg-user-1", payload, DateTimeOffset.UtcNow);

        var loaded = Reload(original.Id);

        loaded.ShouldNotBeNull();
        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<TextContent>();

        var text = (TextContent)loaded.ContentPayload!;
        text.Text.ShouldBe("Hello from Telegram!");
        text.Format.ShouldBe("plain");
        text.ContentType.ShouldBe("text");
    }

    [Fact]
    public void VoiceContent_PersistAndReload_PreservesPayload()
    {
        var payload = new VoiceContent(
            "Transcribed voice", "https://audio.example.com/clip.ogg", 5.2, "audio/ogg")
        {
            CreatedAt = new DateTimeOffset(2026, 3, 6, 15, 0, 0, TimeSpan.Zero),
        };

        var original = CreateAndPersist(
            ChannelType.WhatsApp, "wa-chat-1", "wa-user-1", payload, DateTimeOffset.UtcNow);

        var loaded = Reload(original.Id);

        loaded.ShouldNotBeNull();
        loaded.ContentPayload.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeOfType<VoiceContent>();

        var voice = (VoiceContent)loaded.ContentPayload!;
        voice.TranscriptionText.ShouldBe("Transcribed voice");
        voice.AudioUrl.ShouldBe("https://audio.example.com/clip.ogg");
        voice.DurationSeconds.ShouldBe(5.2);
        voice.MimeType.ShouldBe("audio/ogg");
    }

    [Fact]
    public void NullContentPayload_PersistAndReload_RemainsNull()
    {
        var original = CreateAndPersist(
            ChannelType.Teams, "teams-chat-1", "teams-user-1", null, DateTimeOffset.UtcNow);

        var loaded = Reload(original.Id);

        loaded.ShouldNotBeNull();
        loaded.ContentPayload.ShouldBeNull();
    }

    [Theory]
    [InlineData(ChannelType.Telegram)]
    [InlineData(ChannelType.Teams)]
    [InlineData(ChannelType.WhatsApp)]
    [InlineData(ChannelType.Signal)]
    [InlineData(ChannelType.WebWidget)]
    public void DifferentChannelTypes_PersistAndReload_PreserveChannelType(ChannelType channelType)
    {
        var original = CreateAndPersist(
            channelType, $"chat-{channelType}", $"user-{channelType}", null, DateTimeOffset.UtcNow);

        var loaded = Reload(original.Id);

        loaded.ShouldNotBeNull();
        loaded.Channel.ShouldBe(channelType);
    }

    [Fact]
    public void MultipleEvents_PersistAndReload_AllDistinct()
    {
        var e1 = CreateAndPersist(
            ChannelType.Telegram, "chat-1", "user-1", new TextContent("msg1"), DateTimeOffset.UtcNow);
        var e2 = CreateAndPersist(
            ChannelType.Teams, "chat-2", "user-2", new TextContent("msg2"), DateTimeOffset.UtcNow);

        var loaded1 = Reload(e1.Id);
        var loaded2 = Reload(e2.Id);

        loaded1.ShouldNotBeNull();
        loaded2.ShouldNotBeNull();
        loaded1.Id.ShouldNotBe(loaded2.Id);
        loaded1.Channel.ShouldBe(ChannelType.Telegram);
        loaded2.Channel.ShouldBe(ChannelType.Teams);
    }
}
