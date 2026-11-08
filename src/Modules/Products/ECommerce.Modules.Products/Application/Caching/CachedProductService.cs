using ECommerce.Modules.Products.Application;
using ECommerce.Modules.Products.Application.Services;
using ECommerce.SharedKernel.Caching;
using ECommerce.SharedKernel.Pagination;
using System.Diagnostics;

namespace ECommerce.Modules.Products.Application.Caching;

internal class CachedProductService(IProductService productService, ICacheStore cacheStore) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query, CancellationToken ct)
    {
        var result = await cacheStore.GetOrSetAsync(
            ProductsCacheKeys.ProductsList(query),
            async () => await productService.GetAllAsync(query, ct),
            TimeSpan.FromMinutes(5),
            ct);

        return result;
    }

    public async Task<ProductDetailsDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await cacheStore.GetOrSetAsync(
            new CacheKey($"products:{id}"),
            async () => await productService.GetByIdAsync(id, ct),
            TimeSpan.FromMinutes(5),
            ct);
    }

    public async Task<ProductDto> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        await cacheStore.RemoveByPrefixAsync("products:page:", ct);
        var result = await productService.CreateAsync(request, ct);
        return result;
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductRequest request, CancellationToken ct)
    {
        await cacheStore.RemoveByPrefixAsync("products:page:", ct);
        await cacheStore.RemoveAsync(new CacheKey($"products:{id}"), ct);
        var result = await productService.UpdateAsync(id, request, ct);
        return result;
    }

    public async Task<ProductStockDto> UpdateStockAsync(Guid id, StockRequest request, CancellationToken ct)
    {
        await cacheStore.RemoveByPrefixAsync("products:page:", ct);
        await cacheStore.RemoveAsync(new CacheKey($"products:{id}"), ct);
        var result = await productService.UpdateStockAsync(id, request, ct);
        return result;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await cacheStore.RemoveByPrefixAsync("products:page:", ct);
        await cacheStore.RemoveAsync(new CacheKey($"products:{id}"), ct);
        await productService.DeleteAsync(id, ct);
    }
}
