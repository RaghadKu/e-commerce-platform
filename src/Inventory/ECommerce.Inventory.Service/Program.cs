using ECommerce.Inventory.Service.Api;
using ECommerce.Inventory.Service.Application;
using ECommerce.Inventory.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.AddDbContext<InventoryDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("InventoryDb")
        ?? throw new InvalidOperationException("Connection string 'InventoryDb' is not configured.");

    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<StockManager>();
builder.Services.AddHostedService<InventoryDbInitializer>();

var app = builder.Build();

app.MapGrpcService<InventoryGrpcService>();

if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

app.Run();

public partial class Program;