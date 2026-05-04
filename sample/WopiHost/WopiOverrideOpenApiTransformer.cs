using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using WopiHost.Core.Infrastructure;

namespace WopiHost;

/// <summary>
/// An OpenAPI operation transformer that adds the X-WOPI-Override header parameter
/// to operations disambiguated by <see cref="WopiOverrideHeaderAttribute"/>.
/// This resolves OpenAPI conflicts where multiple POST actions share the same route
/// but are distinguished at runtime by the X-WOPI-Override header value.
/// </summary>
public sealed class WopiOverrideOpenApiTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var overrideAttr = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<WopiOverrideHeaderAttribute>()
            .FirstOrDefault();

        if (overrideAttr is null)
        {
            return Task.CompletedTask;
        }

        // Add the X-WOPI-Override header parameter with accepted values as enum
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = WopiHeaders.WOPI_OVERRIDE,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Specifies the requested WOPI operation.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. overrideAttr.Values.Select(v => (JsonNode)JsonValue.Create(v)!)]
            }
        });

        // Set a unique operationId based on the action method name to avoid conflicts
        var actionName = context.Description.ActionDescriptor.RouteValues.TryGetValue("action", out var name)
            ? name
            : null;

        if (actionName is not null)
        {
            operation.OperationId = actionName;
        }

        return Task.CompletedTask;
    }
}
