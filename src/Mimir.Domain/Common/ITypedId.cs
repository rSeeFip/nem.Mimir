namespace Mimir.Domain.Common;

public interface ITypedId<T> where T : struct
{
    T Value { get; init; }
}
