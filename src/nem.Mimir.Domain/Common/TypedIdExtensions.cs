using System.Reflection;

namespace nem.Mimir.Domain.Common;

/// <summary>
/// Extension methods for discovering and scanning typed ID implementations.
/// </summary>
public static class TypedIdExtensions
{
    /// <summary>
    /// Scans an assembly for all types implementing ITypedId{T} and returns their metadata.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>
    /// A read-only list of tuples containing:
    /// - IdType: The typed ID type (e.g., UserId)
    /// - ValueType: The underlying value type from the generic argument (e.g., Guid)
    /// </returns>
    public static IReadOnlyList<(Type IdType, Type ValueType)> GetTypedIds(this Assembly assembly)
        => assembly.GetTypedIds(typeof(ITypedId<>));

    public static IReadOnlyList<(Type IdType, Type ValueType)> GetTypedIds(this Assembly assembly, Type typedIdInterfaceType)
    {
        ArgumentNullException.ThrowIfNull(typedIdInterfaceType);

        if (!typedIdInterfaceType.IsInterface || !typedIdInterfaceType.IsGenericTypeDefinition || typedIdInterfaceType.GetGenericArguments().Length != 1)
        {
            throw new ArgumentException("typedIdInterfaceType must be an open generic interface with one generic argument.", nameof(typedIdInterfaceType));
        }

        return assembly.GetTypes()
            .Where(t => t.IsValueType && !t.IsAbstract)
            .Select(t => (Type: t, Interface: t.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typedIdInterfaceType)))
            .Where(x => x.Interface is not null)
            .Select(x => (x.Type, x.Interface!.GetGenericArguments()[0]))
            .ToList();
    }
}
