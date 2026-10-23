using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using WithSaga.Orchestrator.Contracts;

namespace WithSaga.Orchestrator.Swagger;

/// <summary>Fills Swagger "Try it out" with the demo payload.</summary>
public sealed class DemoSchemaExamples : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }

        if (context.Type == typeof(CreateOrderRequest))
        {
            concrete.Example = JsonNode.Parse("""{"productId":"A","quantity":1,"amount":49.99}""");
        }
    }
}
