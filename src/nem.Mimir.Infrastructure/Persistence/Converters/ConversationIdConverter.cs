namespace nem.Mimir.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Mimir.Domain.ValueObjects;

public class ConversationIdConverter : ValueConverter<ConversationId, Guid>
{
    public ConversationIdConverter()
        : base(
            id => id.Value,
            guid => ConversationId.From(guid))
    {
    }
}
