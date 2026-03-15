namespace nem.Mimir.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Mimir.Domain.ValueObjects;

public class UserIdConverter : ValueConverter<UserId, Guid>
{
    public UserIdConverter()
        : base(
            id => id.Value,
            guid => UserId.From(guid))
    {
    }
}
