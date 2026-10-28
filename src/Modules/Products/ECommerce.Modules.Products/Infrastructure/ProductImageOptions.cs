using System.ComponentModel.DataAnnotations;

namespace ECommerce.Modules.Products.Infrastructure;

internal sealed class ProductImageOptions
{
    public const string SectionName = "ProductImageOptions";
    
    [Required] public string RootPath { get; set; } = "Uploads/Products";
    [Range(1024, 10 * 1024 * 1024)] public int MaxSizeInBytes { get; set; } = 2 * 1024 * 1024;
    [Range(1, 20)] public int MaxImagesPerProduct { get; set; } = 5;
}
