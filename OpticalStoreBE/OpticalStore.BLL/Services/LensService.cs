using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Lenses;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class LensService : ILensService
{
    private readonly OpticalStoreDbContext _dbContext;

    // Khoi tao service lens voi db context.
    public LensService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Tao lens moi trong he thong.
    public async Task<LensResponseDto> CreateAsync(CreateLensDto request, CancellationToken cancellationToken = default)
    {
        var lens = new Len
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Material = request.Material,
            Price = request.Price,
            Description = request.Description,
            IsDeleted = false
        };

        _dbContext.Lens.Add(lens);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(lens);
    }

    // Lay toan bo lens chua bi xoa.
    public async Task<List<LensResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var data = await _dbContext.Lens
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);

        return data.Select(Map).ToList();
    }

    // Lay lens theo id va kiem tra ton tai.
    public async Task<LensResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var lens = await _dbContext.Lens.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (lens is null)
        {
            throw new AppException("LENS_NOT_FOUND", "Lens not found.", HttpStatusCode.NotFound);
        }

        return Map(lens);
    }

    // Cap nhat thong tin lens.
    public async Task<LensResponseDto> UpdateAsync(string id, CreateLensDto request, CancellationToken cancellationToken = default)
    {
        var lens = await _dbContext.Lens.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (lens is null)
            throw new AppException("LENS_NOT_FOUND", "Lens not found.", HttpStatusCode.NotFound);

        lens.Name = request.Name;
        lens.Material = request.Material;
        lens.Price = request.Price;
        lens.Description = request.Description;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(lens);
    }

    // Xoa mem lens bang cach danh dau IsDeleted.
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var lens = await _dbContext.Lens.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (lens is null)
            throw new AppException("LENS_NOT_FOUND", "Lens not found.", HttpStatusCode.NotFound);

        lens.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Chuyen entity lens sang DTO tra ve API.
    private static LensResponseDto Map(Len lens)
    {
        return new LensResponseDto
        {
            Id = lens.Id,
            Name = lens.Name,
            Material = lens.Material,
            Price = lens.Price,
            Description = lens.Description
        };
    }
}
