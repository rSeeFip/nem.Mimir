using nem.Contracts.Identity;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public class ChannelTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_Channel()
    {
        var ownerId = Guid.NewGuid();

        var channel = Channel.Create(ownerId, "engineering", "Core engineering discussions", ChannelType.Public);

        channel.Id.ShouldNotBe(ChannelId.Empty);
        channel.OwnerId.ShouldBe(ownerId);
        channel.Name.ShouldBe("engineering");
        channel.Description.ShouldBe("Core engineering discussions");
        channel.Type.ShouldBe(ChannelType.Public);
        channel.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_Should_Raise_ChannelCreatedEvent()
    {
        var ownerId = Guid.NewGuid();
        var channel = Channel.Create(ownerId, "general", null, ChannelType.Public);

        var createdEvent = channel.DomainEvents.OfType<ChannelCreatedEvent>().ShouldHaveSingleItem();
        createdEvent.ChannelId.ShouldBe(channel.Id);
        createdEvent.OwnerId.ShouldBe(ownerId);
    }

    [Fact]
    public void AddMember_Should_Add_Member_And_Raise_Event()
    {
        var channel = Channel.Create(Guid.NewGuid(), "general", null, ChannelType.Public);
        var memberId = Guid.NewGuid();

        channel.AddMember(memberId);

        channel.Members.ShouldContain(member => member.UserId == memberId);
        channel.DomainEvents.OfType<MemberJoinedEvent>().ShouldContain(e => e.UserId == memberId);
    }

    [Fact]
    public void AddMessage_Should_Create_Message_And_Raise_Event()
    {
        var senderId = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "general", null, ChannelType.Public);
        channel.AddMember(senderId);

        var message = channel.AddMessage(senderId, "Hello channel!");

        message.ChannelId.ShouldBe(channel.Id);
        message.SenderId.ShouldBe(senderId);
        message.Content.ShouldBe("Hello channel!");
        channel.DomainEvents.OfType<ChannelMessageSentEvent>().ShouldContain(e => e.MessageId == message.Id);
    }
}
