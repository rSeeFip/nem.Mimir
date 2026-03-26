namespace nem.Mimir.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class ContractsTypedIdValueConverter<TId, TValue> : ValueConverter<TId, TValue>
    where TId : struct, nem.Contracts.Identity.ITypedId<TValue>
    where TValue : struct
{
    public ContractsTypedIdValueConverter()
        : base(
            id => id.Value,
            value => (TId)Activator.CreateInstance(typeof(TId), value)!)
    {
    }
}
