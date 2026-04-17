namespace OpticalStore.API.Responses;

public sealed class ApiResponse<T>
{
    public int Code { get; set; } = 1000;

    public string? Message { get; set; }

    public T? Result { get; set; }
}
