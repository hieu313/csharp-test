namespace OrderManagement.Dtos;

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
