namespace ECommerce.Modules.Products.Domain;

internal sealed class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
