using System.Text.Json.Nodes;
using Bayubai.Identity;
using Bayubai.SharedKernel.Errors;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bayubai.Api;

// Publishes every error code as an enum so the generated client and the translation tests see the full list.
internal static class ErrorCodeSchemaTransformer
{
    public const string SchemaName = "ErrorCode";

    public static Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[SchemaName] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = CommonErrors.Codes.Concat(IdentityModule.ErrorCodes).Order(StringComparer.Ordinal).Select(code => (JsonNode)code).ToList(),
        };
        return Task.CompletedTask;
    }
}
