namespace OrderManagementAPI.Commands;

public sealed record UpdateOrderDetailCommand(
    Guid Id,
    int? Quantity,
    decimal? Discount,
    string? Notes
);
