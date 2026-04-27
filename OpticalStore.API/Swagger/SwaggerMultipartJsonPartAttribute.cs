namespace OpticalStore.API.Swagger;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class SwaggerMultipartJsonPartAttribute : Attribute
{
    public SwaggerMultipartJsonPartAttribute(string partName, Type schemaType)
    {
        PartName = partName;
        SchemaType = schemaType;
    }

    public string PartName { get; }

    public Type SchemaType { get; }

    public bool Required { get; set; } = true;
}
