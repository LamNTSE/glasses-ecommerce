namespace OpticalStore.BLL.DTOs.Common;

public sealed class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();

    public int Page { get; set; }

    public int Size { get; set; }

    public long TotalElements { get; set; }

    public int TotalPages { get; set; }
}
