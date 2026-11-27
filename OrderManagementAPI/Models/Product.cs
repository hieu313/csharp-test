namespace OrderManagement;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Product() { }

    public Product(string name, string sku, decimal price, int stock)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (stock < 0) throw new ArgumentOutOfRangeException(nameof(stock));

        Id = Guid.NewGuid();
        Name = name;
        Sku = sku;
        Price = price;
        Stock = stock;
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    // Dùng khi entity đã được tracking và dòng đã bị khóa.
    // Service phải kiểm tra đủ stock trước; đây chỉ là lớp bảo vệ cuối.
    public void DecreaseStock(int quantity)
    {
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (quantity > Stock) throw new InvalidOperationException($"Insufficient stock for product {Id}");
        Stock -= quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        Stock += quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}