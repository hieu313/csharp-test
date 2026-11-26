using OrderManagement.Dtos;
using Microsoft.EntityFrameworkCore;

namespace OrderManagement.Services;

public interface IOrderDetailService
{
    Task<OrderDetailResponseDto?> GetByIdAsync(Guid id);
}

public class OrderDetailService : IOrderDetailService
{
    private readonly AppDbContext _db;
    public OrderDetailService(AppDbContext db) => _db = db;

    public async Task<OrderDetailResponseDto?> GetByIdAsync(Guid id)
    {
        return await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.Id == id)
            .Select(od => new OrderDetailResponseDto(
                od.Id,
                od.OrderId,
                od.ProductId,
                od.Product.Name,
                od.Product.Sku,
                od.Quantity,
                od.UnitPrice,
                od.Discount,
                od.Subtotal,
                od.Notes,
                od.CreatedAt,
                od.UpdatedAt))
            .FirstOrDefaultAsync();
    }
}
