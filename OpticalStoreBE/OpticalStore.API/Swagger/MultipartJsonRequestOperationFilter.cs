using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpticalStore.API.Swagger;

public sealed class MultipartJsonRequestOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attributes = context.MethodInfo.GetCustomAttributes(typeof(SwaggerMultipartJsonPartAttribute), true)
            .OfType<SwaggerMultipartJsonPartAttribute>()
            .ToArray();

        if (attributes.Length == 0 || operation.RequestBody is null)
        {
            return;
        }

        if (!operation.RequestBody.Content.TryGetValue("multipart/form-data", out var mediaType))
        {
            return;
        }

        mediaType.Schema ??= new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>(),
            Required = new HashSet<string>()
        };

        mediaType.Schema.Properties ??= new Dictionary<string, OpenApiSchema>();
        mediaType.Schema.Required ??= new HashSet<string>();
        mediaType.Encoding ??= new Dictionary<string, OpenApiEncoding>();

        foreach (var attribute in attributes)
        {
            var existingPropertyName = mediaType.Schema.Properties.Keys
                .FirstOrDefault(x => string.Equals(x, attribute.PartName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(existingPropertyName)
                && !string.Equals(existingPropertyName, attribute.PartName, StringComparison.Ordinal))
            {
                mediaType.Schema.Properties.Remove(existingPropertyName);
                mediaType.Schema.Required.Remove(existingPropertyName);
                mediaType.Encoding.Remove(existingPropertyName);
            }

            var jsonSchema = context.SchemaGenerator.GenerateSchema(attribute.SchemaType, context.SchemaRepository);

            mediaType.Schema.Properties[attribute.PartName] = jsonSchema;
            mediaType.Encoding[attribute.PartName] = new OpenApiEncoding
            {
                ContentType = "application/json"
            };

            if (attribute.Required)
            {
                mediaType.Schema.Required.Add(attribute.PartName);
            }
        }
    }
}
