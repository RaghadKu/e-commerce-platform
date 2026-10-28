using ECommerce.SharedKernel.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ECommerce.Modules.Products.Infrastructure;

internal class LocalImageStorage : IImageStorage
{
    private readonly string _root;

    public LocalImageStorage(IOptions<ProductImageOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.RootPath;

        _root = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured));
        
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        await using var target = new FileStream(
            FullPath(fileName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        await content.CopyToAsync(target, cancellationToken);
    }

    public Stream Open(string fileName)
    {
        var path = FullPath(fileName);

        if (!File.Exists(path))
            throw new NotFoundException("The image file was not found.");

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    public void Delete(string fileName)
    {
        var path = FullPath(fileName);

        if (File.Exists(path)) File.Delete(path);
    }

    private string FullPath(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(_root, fileName));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid image path.");

        return path;
    }
}
