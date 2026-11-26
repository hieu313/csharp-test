namespace OrderManagement;

public class OrderDetail
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Subtotal { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Order Order { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    private OrderDetail() { }

    public OrderDetail(Guid orderId, Guid productId, int quantity, decimal unitPrice,
        decimal discount = 0, string? notes = null)
    {
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (discount < 0) throw new ArgumentOutOfRangeException(nameof(discount));

        var subtotal = quantity * unitPrice - discount;
        if (subtotal < 0) throw new ArgumentException("Discount exceeds line total.", nameof(discount));

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Discount = discount;
        Subtotal = subtotal;
        Notes = notes;
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    public void Update(int? quantity, decimal? discount, string? notes)
    {
        if (quantity.HasValue)
        {
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            Quantity = quantity.Value;
        }
        if (discount.HasValue)
        {
            if (discount < 0) throw new ArgumentOutOfRangeException(nameof(discount));
            Discount = discount.Value;
        }
        if (notes != null) Notes = notes;

        var subtotal = Quantity * UnitPrice - Discount;
        if (subtotal < 0) throw new ArgumentException("Discount exceeds line total.", nameof(discount));
        Subtotal = subtotal;
        UpdatedAt = DateTime.UtcNow;
    }
}