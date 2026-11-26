using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Dtos;

public class OrderDetailItemRequest
{
    [Required] public Guid? ProductId { get; set; }
    [Required, Range(1, int.MaxValue)] public int? Quantity { get; set; }
    [Range(0, double.MaxValue)] public decimal? Discount { get; set; }
}

public class CreateOrderDetailRequest : OrderDetailItemRequest
{
    [Required] public Guid? OrderId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class CreateBulkOrderDetailRequest
{
    [Required] public Guid? OrderId { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<OrderDetailItemRequest> Items { get; set; } = new();
}

public class UpdateOrderDetailRequest
{
    [Range(1, int.MaxValue)] public int? Quantity { get; set; }
    [Range(0, double.MaxValue)] public decimal? Discount { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public record OrderDetailResponseDto(
  Guid Id,
  Guid OrderId,
  Guid ProductId,
  string ProductName,
  string ProductSku,
  int Quantity,
  decimal UnitPrice,
  decimal Discount,
  decimal Subtotal,
  string? Notes,
  DateTime CreatedAt,
  DateTime UpdatedAt
);

// Lỗi của 1 item trong bulk: index = vị trí trong mảng items (bắt đầu từ 0)
public record BulkItemError(int Index, Guid ProductId, string Error);

public record OrderSummary(int TotalItems, int TotalQuantity, decimal TotalAmount);

public record OrderDetailsByOrderResponse(
    IReadOnlyList<OrderDetailResponseDto> Data,
    OrderSummary Summary);

public record ProductSalesSummary(int TotalOrders, int TotalQuantitySold, decimal TotalRevenue);

public record OrderDetailsByProductResponse(
    IReadOnlyList<OrderDetailResponseDto> Data,
    ProductSalesSummary Summary);
