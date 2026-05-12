using System.Text.Json;
using System.Text.Json.Serialization;
using nem.Mimir.Domain.Common;

namespace nem.Mimir.Infrastructure.Serialization;

/// <summary>
/// JSON converter for typed IDs that wrap Guid values.
/// </summary>
/// <typeparam name="TId">The typed ID type, must implement ITypedId&lt;Guid&gt;</typeparam>
public sealed class TypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, ITypedId<Guid>
{
    /// <summary>
    /// Reads a JSON token and converts it to a typed ID.
    /// </summary>
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var guid = reader.GetGuid();
        return new TId { Value = guid };
    }

    /// <summary>
    /// Writes a typed ID to JSON as a string representation of its Guid value.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Factory for creating TypedIdJsonConverter instances for any ITypedId&lt;Guid&gt; type.
/// This factory enables automatic converter discovery and registration.
/// </summary>
public sealed class TypedIdJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// Determines if the factory can convert the specified type.
    /// Returns true if the type is a value type implementing ITypedId&lt;Guid&gt;.
    /// </summary>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsValueType &&
            typeToConvert.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<>));
    }

    /// <summary>
    /// Creates a TypedIdJsonConverter instance for the specified type.
    /// </summary>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(TypedIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
