using ECommerce.Modules.Products.Domain;

namespace ECommerce.Modules.Products.Application.Mapping;

internal static class ProductsMapping
{
    public static CategoryDto ToDto(this Category c)
    {
        return new(c.Id, c.Name, c.Description);
    }

    public static ProductImageDto ToDto(this ProductImage i)
    {
        return new(i.Id, $"/api/products/{i.ProductId}/images/{i.Id}", i.ContentType, i.SizeInBytes);
    }

    public static ProductDto ToDto(this Product p)
    {
        return new(
            p.Id, p.Name, p.Description, p.Price, p.Sku, p.CategoryId, p.Category.Name,
            p.Images.OrderBy(i => i.CreatedAtUtc).Select(i => i.ToDto()).ToList(),
            p.CreatedAtUtc, p.UpdatedAtUtc);
    }
}
