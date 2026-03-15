namespace nem.Mimir.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Mimir.Domain.ValueObjects;

public class MessageIdConverter : ValueConverter<MessageId, Guid>
{
    public MessageIdConverter()
        : base(
            id => id.Value,
            guid => MessageId.From(guid))
    {
    }
}
