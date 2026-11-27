namespace OrderManagementAPI.Commands;

public sealed record BulkOrderDetailItem(
    Guid ProductId,
    int Quantity,
    decimal Discount,
    string? Notes
);

public sealed record CreateBulkOrderDetailCommand(
    Guid OrderId,
    IReadOnlyList<BulkOrderDetailItem> Items
);