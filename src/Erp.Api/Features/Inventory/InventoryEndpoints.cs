using Erp.Api.Data;
using Erp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Features.Inventory;

/// <summary>
/// Read-only inventory endpoints: stock on hand and reserved per product and warehouse.
/// </summary>
public static class InventoryEndpoints
{
    /// <summary>Maps the <c>/inventory</c> read endpoints.</summary>
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/inventory").WithTags("Inventory");

        group.MapGet("/{productId:int}", GetInventoryByProduct)
            .WithName("GetInventoryByProduct")
            .Produces<ProductInventoryDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", SearchInventory)
            .WithName("SearchInventory")
            .Produces<IReadOnlyList<ProductInventoryDto>>();

        return app;
    }

    /// <summary>Returns the stock of a single product in every warehouse, plus the total available quantity.</summary>
    /// <param name="productId">Product id.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The product exists and its stock is returned.</response>
    /// <response code="404">No product with the given id exists.</response>
    private static async Task<IResult> GetInventoryByProduct(int productId, ErpDbContext db, CancellationToken ct)
    {
        var results = await LoadInventory(db.Products.Where(p => p.Id == productId), ct);

        return results.Count == 0
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found",
                detail: $"No product with id {productId} exists.")
            : Results.Ok(results[0]);
    }

    /// <summary>Returns stock for every product whose name contains the supplied text.</summary>
    /// <param name="productName">Optional case-insensitive substring of the product name. When omitted, all products are returned.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Zero or more matching products, each with its per-warehouse stock.</response>
    private static async Task<IResult> SearchInventory(
        string? productName,
        ErpDbContext db,
        CancellationToken ct)
    {
        var query = db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(productName))
        {
            var needle = productName.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(needle));
        }

        return Results.Ok(await LoadInventory(query, ct));
    }

    /// <summary>
    /// Projects products and their warehouse stock to DTOs. Total available is
    /// <c>QuantityOnHand - QuantityReserved</c> summed across all warehouses.
    /// </summary>
    private static async Task<List<ProductInventoryDto>> LoadInventory(
        IQueryable<Product> products,
        CancellationToken ct)
    {
        var rows = await products
            .AsNoTracking()
            .OrderBy(p => p.Sku)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Name,
                p.Unit,
                Warehouses = p.Inventory
                    .OrderBy(i => i.Warehouse.Name)
                    .Select(i => new
                    {
                        i.WarehouseId,
                        WarehouseName = i.Warehouse.Name,
                        i.Warehouse.City,
                        i.QuantityOnHand,
                        i.QuantityReserved
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        return rows
            .Select(p => new ProductInventoryDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Unit,
                p.Warehouses.Sum(w => w.QuantityOnHand - w.QuantityReserved),
                p.Warehouses
                    .Select(w => new WarehouseStockDto(
                        w.WarehouseId,
                        w.WarehouseName,
                        w.City,
                        w.QuantityOnHand,
                        w.QuantityReserved,
                        w.QuantityOnHand - w.QuantityReserved))
                    .ToList()))
            .ToList();
    }
}

/// <summary>Inventory of one product across all warehouses.</summary>
/// <param name="ProductId">Product id.</param>
/// <param name="Sku">Product stock keeping unit.</param>
/// <param name="ProductName">Product name.</param>
/// <param name="Unit">Unit of measure (for example <c>pcs</c> or <c>reel</c>).</param>
/// <param name="TotalAvailable">Total available quantity across all warehouses (<c>on hand - reserved</c>).</param>
/// <param name="Warehouses">Per-warehouse stock breakdown.</param>
public sealed record ProductInventoryDto(
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    int TotalAvailable,
    IReadOnlyList<WarehouseStockDto> Warehouses);

/// <summary>Stock of one product in one warehouse.</summary>
/// <param name="WarehouseId">Warehouse id.</param>
/// <param name="WarehouseName">Warehouse name.</param>
/// <param name="City">City the warehouse is located in.</param>
/// <param name="QuantityOnHand">Units physically on hand.</param>
/// <param name="QuantityReserved">Units already reserved by orders.</param>
/// <param name="Available">Available units (<c>on hand - reserved</c>).</param>
public sealed record WarehouseStockDto(
    int WarehouseId,
    string WarehouseName,
    string City,
    int QuantityOnHand,
    int QuantityReserved,
    int Available);
