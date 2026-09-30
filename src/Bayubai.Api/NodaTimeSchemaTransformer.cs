using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NodaTime;

namespace Bayubai.Api;

// NodaTime serializes Instant as an ISO-8601 string; without this the document shows it as an object.
internal static class NodaTimeSchemaTransformer
{
    public static Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(Instant) || type == typeof(Instant?))
        {
            schema.Type = type == typeof(Instant) ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
            schema.Format = "date-time";
            schema.Properties?.Clear();
        }

        return Task.CompletedTask;
    }
}
