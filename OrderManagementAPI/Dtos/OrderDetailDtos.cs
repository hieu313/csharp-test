using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Dtos;

public class CreateOrderDetailRequest
{
    [Required] public Guid? OrderId { get; set; }
    [Required] public Guid? ProductId { get; set; }
    [Required, Range(1, int.MaxValue)] public int? Quantity { get; set; }
    [Range(0, double.MaxValue)] public decimal? Discount { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
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

public record OrderSummary(int TotalItems, int TotalQuantity, decimal TotalAmount);

public record OrderDetailsByOrderResponse(
    IReadOnlyList<OrderDetailResponseDto> Data,
    OrderSummary Summary);
