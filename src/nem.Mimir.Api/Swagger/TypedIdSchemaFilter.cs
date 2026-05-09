using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using nem.Mimir.Domain.Common;

namespace nem.Mimir.Api.Swagger;

/// <summary>
/// Schema filter for Swagger that converts typed ID types to UUID format strings.
/// </summary>
public sealed class TypedIdSchemaFilter : ISchemaFilter
{
    /// <summary>
    /// Applies the schema filter to convert ITypedId&lt;Guid&gt; implementations to uuid format strings.
    /// </summary>
    /// <param name="schema">The OpenAPI schema to modify.</param>
    /// <param name="context">The schema filter context containing type information.</param>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Check if the type implements ITypedId<Guid>
        if (IsTypedId(context.Type))
        {
            dynamic s = schema;
            s.Type = "string";
            s.Format = "uuid";
            s.Properties?.Clear();
            s.Reference = null;
        }
    }

    /// <summary>
    /// Determines whether a type implements ITypedId&lt;Guid&gt;.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type implements ITypedId&lt;Guid&gt;; otherwise, false.</returns>
    private static bool IsTypedId(Type type)
    {
        if (type == null)
        {
            return false;
        }

        // Check if the type directly implements ITypedId<Guid>
        var typedIdInterface = type.GetInterfaces()
            .FirstOrDefault(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(ITypedId<>) &&
                i.GetGenericArguments()[0] == typeof(Guid));

        return typedIdInterface != null;
    }
}
