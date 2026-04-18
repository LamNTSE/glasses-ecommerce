using OpticalStore.BLL.DTOs.Combos;
using OpticalStore.BLL.DTOs.Common;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IComboWorkflowService
{
    Task<object> CreateAsync(ComboUpsertDto request, CancellationToken cancellationToken = default);

    Task<object> UpdateAsync(string comboId, ComboUpsertDto request, CancellationToken cancellationToken = default);

    Task<object> UpdateStatusAsync(string comboId, string status, CancellationToken cancellationToken = default);

    Task<PagedResultDto<object>> GetAllAsync(
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int size,
        string sortBy,
        string sortDir,
        CancellationToken cancellationToken = default);

    Task<object> GetByIdAsync(string comboId, CancellationToken cancellationToken = default);

    Task<List<object>> GetAvailableAsync(DateTime? currentTime, CancellationToken cancellationToken = default);

    Task<object> ValidateAsync(ComboValidateDto request, CancellationToken cancellationToken = default);

    Task<object> CheckStockAsync(string comboId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string comboId, CancellationToken cancellationToken = default);
}
