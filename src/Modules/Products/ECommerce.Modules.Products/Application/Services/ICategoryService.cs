namespace ECommerce.Modules.Products.Application.Services
{
    public interface ICategoryService
    {
        Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct);
        Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct);
        Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct);
        Task<CategoryDto> UpdateAsync(Guid id, CategoryRequest request, CancellationToken ct);
        Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
