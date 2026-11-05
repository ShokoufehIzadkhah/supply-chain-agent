using Erp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Data;

public class ErpDbContext(DbContextOptions<ErpDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Inventory> Inventory => Set<Inventory>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(builder =>
        {
            builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
            builder.Property(c => c.Country).HasMaxLength(100).IsRequired();
            builder.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(builder =>
        {
            builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
            builder.Property(p => p.Category).HasMaxLength(100).IsRequired();
            builder.Property(p => p.Unit).HasMaxLength(20).IsRequired();
            builder.HasIndex(p => p.Sku).IsUnique();
        });

        modelBuilder.Entity<Warehouse>(builder =>
        {
            builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
            builder.Property(w => w.City).HasMaxLength(100).IsRequired();
            builder.HasIndex(w => w.Name).IsUnique();
        });

        modelBuilder.Entity<Inventory>(builder =>
        {
            // Stock is identified by the product/warehouse pair, so it uses a composite key.
            builder.HasKey(i => new { i.ProductId, i.WarehouseId });

            builder.HasOne(i => i.Product)
                .WithMany(p => p.Inventory)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.Warehouse)
                .WithMany(w => w.Inventory)
                .HasForeignKey(i => i.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(builder =>
        {
            // Order ids are issued by the ERP starting at 1001 (see the InitialCreate migration).
            builder.Property(o => o.Id).UseAutoincrement();
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.Status);

            builder.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderLine>(builder =>
        {
            // An order can only reference a product once, so the key is the order/product pair.
            builder.HasKey(l => new { l.OrderId, l.ProductId });

            builder.Property(l => l.UnitPrice).HasPrecision(18, 2);

            builder.HasOne(l => l.Order)
                .WithMany(o => o.Lines)
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(l => l.Product)
                .WithMany(p => p.OrderLines)
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Shipment>(builder =>
        {
            builder.Property(s => s.Carrier).HasMaxLength(100).IsRequired();
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(s => s.TrackingNumber).HasMaxLength(100);

            builder.HasIndex(s => s.OrderId);
            builder.HasIndex(s => s.TrackingNumber).IsUnique();

            builder.HasOne(s => s.Order)
                .WithMany(o => o.Shipments)
                .HasForeignKey(s => s.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
