using ECommerce.Modules.Products.Application.Mapping;
using ECommerce.Modules.Products.Domain;
using ECommerce.Modules.Products.Infrastructure;
using ECommerce.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Application.Services;

internal sealed class CategoryService(ProductsDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct)
    {
        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return (await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found.")).ToDto();
    }

    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();

        if (await db.Categories.AnyAsync(c => c.Name == name, ct))
            throw new ConflictException("A category with this name already exists.");

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim()
        };

        db.Categories.Add(category);

        await db.SaveOrConflictAsync(ct);

        return category.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found.");

        var name = request.Name.Trim();

        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            throw new ConflictException("A category with this name already exists.");

        category.Name = name;
        category.Description = request.Description?.Trim();
        await db.SaveOrConflictAsync(ct);

        return category.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
               ?? throw new NotFoundException("Category not found.");

        if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.CategoryId == id, ct))
            throw new ConflictException("The category still has products and cannot be deleted.");

        db.Categories.Remove(category);

        await db.SaveOrConflictAsync(ct);
    }
}
