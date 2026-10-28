namespace ECommerce.Modules.Products.Domain;

internal sealed class ProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}