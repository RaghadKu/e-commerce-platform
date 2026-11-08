using ECommerce.Modules.Products.Application;
using ECommerce.SharedKernel.Caching;

namespace ECommerce.Modules.Products.Application.Caching;

internal static class ProductsCacheKeys
{
    public static CacheKey ProductsList(ProductQuery query)
        => new($"products:page:{query.Page}:pageSize:{query.PageSize}:categoryId:{query.CategoryId}:search:{query.Search}");
}
