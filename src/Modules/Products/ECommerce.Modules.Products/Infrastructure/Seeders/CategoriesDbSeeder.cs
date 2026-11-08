using ECommerce.Modules.Products.Domain;
using ECommerce.Modules.Products.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Infrastructure.Seeders;

internal sealed class CategoriesDbSeeder(
    ProductsDbContext db) : ICategoriesDbSeeder
{
    private static readonly (string Name, string Description)[] Categories =
    [
        ("Electronics", "Electronic devices and accessories."),
        ("Computers", "Computers and computing equipment."),
        ("Laptops", "Portable computers and laptops."),
        ("Desktops", "Desktop computers and workstations."),
        ("Computer Accessories", "Accessories for computers and peripherals."),
        ("Smartphones", "Mobile phones and smartphone accessories."),
        ("Tablets", "Tablets and related accessories."),
        ("Audio", "Audio equipment and sound accessories."),
        ("Headphones", "Headphones and earbuds."),
        ("Cameras", "Digital cameras and photography equipment."),
        ("Gaming", "Video games and gaming products."),
        ("Gaming Accessories", "Gaming controllers and accessories."),
        ("Monitors", "Computer monitors and displays."),
        ("Printers", "Printers and printing supplies."),
        ("Networking", "Networking equipment and accessories."),
        ("Storage", "Storage devices and memory products."),
        ("Home Appliances", "Appliances for everyday household use."),
        ("Kitchen Appliances", "Kitchen equipment and appliances."),
        ("Furniture", "Indoor and outdoor furniture."),
        ("Home Decor", "Decorative items for the home."),
        ("Lighting", "Indoor and outdoor lighting products."),
        ("Bedding", "Bedding and bedroom accessories."),
        ("Clothing", "Clothing and fashion products."),
        ("Men's Clothing", "Clothing and accessories for men."),
        ("Women's Clothing", "Clothing and accessories for women."),
        ("Kids' Clothing", "Clothing and accessories for children."),
        ("Shoes", "Footwear for different occasions."),
        ("Sportswear", "Clothing designed for sports and exercise."),
        ("Watches", "Wristwatches and watch accessories."),
        ("Jewelry", "Jewelry and personal accessories."),
        ("Bags", "Bags, backpacks and travel luggage."),
        ("Beauty", "Beauty products and cosmetics."),
        ("Skincare", "Skin care and treatment products."),
        ("Haircare", "Hair care products and accessories."),
        ("Personal Care", "Everyday personal care products."),
        ("Health", "General health and wellness products."),
        ("Fitness", "Fitness and exercise equipment."),
        ("Sports Equipment", "Equipment for different sports."),
        ("Outdoor Gear", "Equipment for outdoor activities."),
        ("Camping", "Camping and hiking equipment."),
        ("Books", "Books and reading materials."),
        ("Stationery", "Writing and office stationery."),
        ("Toys", "Toys and entertainment products."),
        ("Baby Products", "Products for babies and infants."),
        ("Pet Supplies", "Products and accessories for pets."),
        ("Automotive", "Automotive accessories and equipment."),
        ("Tools", "Hand tools and power tools."),
        ("Garden", "Gardening tools and supplies."),
        ("Office Supplies", "Office equipment and supplies."),
        ("Musical Instruments", "Musical instruments and accessories.")
    ];

    public async Task<IReadOnlyList<Guid>> SeedAsync(
        CancellationToken cancellationToken)
    {
        var existingCategories = await db.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var existingNames = existingCategories
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newCategories = Categories
            .Where(c => !existingNames.Contains(c.Name))
            .Select(c => new Category
            {
                Id = Guid.NewGuid(),
                Name = c.Name,
                Description = c.Description
            })
            .ToList();

        if (newCategories.Count > 0)
        {
            db.Categories.AddRange(newCategories);

            await db.SaveChangesAsync(cancellationToken);
        }

        return await db.Categories
            .AsNoTracking()
            .Where(c => Categories.Select(x => x.Name).Contains(c.Name))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }
}