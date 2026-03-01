namespace Mimir.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mimir.Domain.Common;

public sealed class TypedIdValueConverter<TId, TValue> : ValueConverter<TId, TValue>
    where TId : ITypedId<TValue>, new()
    where TValue : struct
{
    public TypedIdValueConverter()
        : base(
            id => id.Value,
            value => new TId { Value = value })
    {
    }
}
