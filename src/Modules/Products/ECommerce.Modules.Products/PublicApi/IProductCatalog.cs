namespace ECommerce.Modules.Products.PublicApi;

public interface IProductCatalog
{
    public sealed record ProductSnapshot(Guid Id, string Name, decimal Price);

    Task<IReadOnlyList<ProductSnapshot>> GetProductsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
