namespace ECommerce.Modules.Products.Application.Services;

public interface IProductImageService
{
    Task<ProductImageDto> AddAsync(Guid productId, Stream content, string originalFileName, long length, CancellationToken ct);
    Task<ProductImageFile> GetAsync(Guid productId, Guid imageId, CancellationToken ct);
    Task DeleteAsync(Guid productId, Guid imageId, CancellationToken ct);
}
