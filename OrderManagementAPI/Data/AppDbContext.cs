using Microsoft.EntityFrameworkCore;

namespace OrderManagement;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }

    //  map đúng snake_case columns và convert enum Status
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.CustomerName).HasColumnName("customer_name");
            e.Property(o => o.Status).HasColumnName("status")
                .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<OrderStatus>(v, true));
            e.Property(o => o.TotalAmount).HasColumnName("total_amount").HasPrecision(15, 2);
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
            e.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name");
            e.Property(p => p.Sku).HasColumnName("sku");
            e.Property(p => p.Price).HasColumnName("price").HasPrecision(15, 2);
            e.Property(p => p.Stock).HasColumnName("stock");
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<OrderDetail>(e =>
        {
            e.Property(od => od.Id).HasColumnName("id");
            e.Property(od => od.OrderId).HasColumnName("order_id");
            e.Property(od => od.ProductId).HasColumnName("product_id");
            e.Property(od => od.Quantity).HasColumnName("quantity");
            e.Property(od => od.UnitPrice).HasColumnName("unit_price").HasPrecision(15, 2);
            e.Property(od => od.Discount).HasColumnName("discount").HasPrecision(15, 2);
            e.Property(od => od.Subtotal).HasColumnName("subtotal").HasPrecision(15, 2);
            e.Property(od => od.Notes).HasColumnName("notes");
            e.Property(od => od.CreatedAt).HasColumnName("created_at");
            e.Property(od => od.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(od => od.Order).WithMany().HasForeignKey(od => od.OrderId)
                .HasConstraintName("FK_OrderDetails_Orders").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(od => od.Product).WithMany().HasForeignKey(od => od.ProductId)
                .HasConstraintName("FK_OrderDetails_Products").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
