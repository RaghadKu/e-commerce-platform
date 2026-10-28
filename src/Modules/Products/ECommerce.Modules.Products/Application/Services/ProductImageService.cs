using ECommerce.Modules.Products.Application.Mapping;
using ECommerce.Modules.Products.Domain;
using ECommerce.Modules.Products.Infrastructure;
using ECommerce.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.Modules.Products.Application.Services;

internal class ProductImageService(
    ProductsDbContext db,
    IImageStorage storage,
    IOptions<ProductImageOptions> options) : IProductImageService
{
    public async Task<ProductImageDto> AddAsync(
        Guid productId, Stream content, string originalFileName, long length, CancellationToken ct)
    {
        var settings = options.Value;

        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
            throw new NotFoundException("Product not found.");

        if (length <= 0)
            throw Invalid("The file is empty.");
        if (length > settings.MaxSizeInBytes)
            throw Invalid($"The file is larger than {settings.MaxSizeInBytes / 1024} KB.");

        if (await db.ProductImages.CountAsync(i => i.ProductId == productId, ct) >= settings.MaxImagesPerProduct)
            throw new ConflictException($"A product can have at most {settings.MaxImagesPerProduct} images.");

        var header = new byte[ImageSignature.HeaderLength];

        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        if (!ImageSignature.TryDetect(header.AsSpan(0, read), out var contentType, out var extension))
            throw Invalid("Only JPEG, PNG, and WebP images are allowed.");

        content.Seek(0, SeekOrigin.Begin);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        await storage.SaveAsync(storedName, content, ct);

        try
        {
            var image = new ProductImage
            {
                ProductId = productId,
                StoredFileName = storedName,
                OriginalFileName = SafeDisplayName(originalFileName),
                ContentType = contentType,
                SizeInBytes = length,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.ProductImages.Add(image);
            await db.SaveChangesAsync(ct);

            return image.ToDto();
        }
        catch
        {
            storage.Delete(storedName);
            throw;
        }
    }

    public async Task<ProductImageFile> GetAsync(Guid productId, Guid imageId, CancellationToken ct)
    {
        var image = await db.ProductImages.AsNoTracking()
                        .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct)
                    ?? throw new NotFoundException("Image not found.");

        return new ProductImageFile(storage.Open(image.StoredFileName), image.ContentType);
    }

    public async Task DeleteAsync(Guid productId, Guid imageId, CancellationToken ct)
    {
        var image = await db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct)
            ?? throw new NotFoundException("Image not found.");

        db.ProductImages.Remove(image);
        await db.SaveChangesAsync(ct);

        storage.Delete(image.StoredFileName);
    }

    private static string SafeDisplayName(string name)
    {
        var fileName = Path.GetFileName(name);
        return fileName.Length > 255 ? fileName[..255] : fileName;
    }

    private static ValidationFailedException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });
}
