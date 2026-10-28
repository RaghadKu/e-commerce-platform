using ECommerce.SharedKernel.Pagination;

namespace ECommerce.Modules.Products.Application.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query, CancellationToken ct);
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ProductDto> CreateAsync(ProductRequest request, CancellationToken ct);
    Task<ProductDto> UpdateAsync(Guid id, ProductRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
