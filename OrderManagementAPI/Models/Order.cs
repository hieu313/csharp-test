namespace OrderManagement;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Completed,
    Cancelled
}

public class Order
{
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Order() { }

    public Order(string customerName)
    {
        Id = Guid.NewGuid();
        CustomerName = customerName;
        Status = OrderStatus.Pending;
        TotalAmount = 0;
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    public bool IsEditable() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;
}