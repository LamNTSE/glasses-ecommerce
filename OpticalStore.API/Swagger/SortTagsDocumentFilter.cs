using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpticalStore.API.Swagger;

public sealed class SortTagsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc?.Tags == null) return;

        swaggerDoc.Tags = swaggerDoc.Tags
            .OrderBy(t => ParseLeadingNumber(t.Name))
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ParseLeadingNumber(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return int.MaxValue;
        var parts = name.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return int.MaxValue;
        return int.TryParse(parts[0].Trim(), out var n) ? n : int.MaxValue;
    }
}
