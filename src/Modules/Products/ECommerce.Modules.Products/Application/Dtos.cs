using System.ComponentModel.DataAnnotations;

namespace ECommerce.Modules.Products.Application;

#region Requests

public sealed class CategoryRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }
}

public sealed class ProductRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Price { get; init; }

    [Required, StringLength(50, MinimumLength = 2)]
    public string Sku { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }
}

public sealed class ProductQuery
{
    public Guid? CategoryId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

#endregion

#region Responses
public sealed record CategoryDto(Guid Id, string Name, string? Description);

public sealed record ProductImageDto(Guid Id, string Url, string ContentType, long SizeBytes);

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Sku,
    Guid CategoryId,
    string CategoryName,
    IReadOnlyList<ProductImageDto> Images,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ProductImageFile(Stream Content, string ContentType);

#endregion
