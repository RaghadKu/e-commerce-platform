namespace ECommerce.Modules.Products.Infrastructure;

internal interface IImageStorage
{
    Task SaveAsync(string fileName, Stream content, CancellationToken cancellationToken);
    Stream Open(string fileName);
    void Delete(string fileName);
}
