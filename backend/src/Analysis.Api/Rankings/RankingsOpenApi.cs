using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Analysis.Api.Rankings;

public static class RankingsOpenApi
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            // Same-origin and reproducible: never bake the export server's port into the SDK.
            document.Servers = [new OpenApiServer { Url = "/" }];
            return Task.CompletedTask;
        });
        options.AddSchemaTransformer((schema, context, _) =>
        {
            // Web JSON's permissive input-number handling otherwise advertises
            // number|string even for output-only counts. M4 emits JSON integers.
            if (context.JsonPropertyInfo?.AttributeProvider is System.Reflection.PropertyInfo property &&
                property.DeclaringType?.Namespace == typeof(RankingsResponse).Namespace)
            {
                var underlying = Nullable.GetUnderlyingType(property.PropertyType);
                var numberType = underlying ?? property.PropertyType;
                if (numberType == typeof(int) || numberType == typeof(long))
                {
                    schema.Type = JsonSchemaType.Integer | (underlying is null ? 0 : JsonSchemaType.Null);
                    schema.Pattern = null;
                    // The bounded age fits a JavaScript safe integer; int64 would
                    // generate a coercing BigInt validator in the pinned generator.
                    schema.Format = numberType == typeof(long) ? null : "int32";
                }
            }
            if (context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(WireAttribute), true)
                .OfType<WireAttribute>().SingleOrDefault() is { } wire)
            { schema.Pattern = wire.Pattern; schema.Format = wire.Format; }
            if (context.JsonTypeInfo.Type.Namespace == typeof(RankingsResponse).Namespace && schema.Properties is not null)
            {
                schema.Required = schema.Properties.Keys.Where(k => context.JsonTypeInfo.Type != typeof(RankingsProblem) ||
                    k is not ("instance" or "code" or "errors")).ToHashSet(StringComparer.Ordinal);
            }
            return Task.CompletedTask;
        });
        options.AddOperationTransformer((operation, _, _) =>
        {
            if (operation.OperationId == "GetRankings")
                foreach (var parameter in operation.Parameters!.OfType<OpenApiParameter>())
                {
                    parameter.Required = false;
                    var parameterSchema = (OpenApiSchema)parameter.Schema!;
                    parameterSchema.Pattern = parameter.Name == "modelId" ? Wire.ModelId : Wire.Hour;
                    if (parameter.Name == "modelId")
                    { parameterSchema.Default = JsonValue.Create(RankingsEndpoint.DefaultModel); parameter.Description = "Exact persisted model identity; never automatically selects a newer model."; }
                    else
                    { parameterSchema.Format = "date-time"; parameter.Description = "Exact stored UTC hour; omission selects greatest persisted as-of for the chosen model."; }
                }
            return Task.CompletedTask;
        });
    }
}
