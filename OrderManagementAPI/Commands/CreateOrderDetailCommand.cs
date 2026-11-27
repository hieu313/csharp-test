namespace OrderManagementAPI.Commands;

public sealed record CreateOrderDetailCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal Discount,
    string? Notes
);
