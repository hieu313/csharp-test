using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Dtos;

namespace OrderManagement.Services;

public interface IOrderDetailService
{
    Task<OrderDetailResponseDto?> GetByIdAsync(Guid id);
    Task<PagedResponse<OrderDetailResponseDto>> GetListAsync(OrderDetailQuery q);
}

public class OrderDetailService : IOrderDetailService
{
    private readonly AppDbContext _db;
    public OrderDetailService(AppDbContext db) => _db = db;

    private static readonly Expression<Func<OrderDetail, OrderDetailResponseDto>> ToDto =
        od => new OrderDetailResponseDto(
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
            od.UpdatedAt);

    public async Task<OrderDetailResponseDto?> GetByIdAsync(Guid id)
    {
        return await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResponse<OrderDetailResponseDto>> GetListAsync(OrderDetailQuery q)
    {
        var query = _db.OrderDetails.AsNoTracking();

        if (q.OrderId.HasValue)
            query = query.Where(od => od.OrderId == q.OrderId.Value);

        if (q.ProductId.HasValue)
            query = query.Where(od => od.ProductId == q.ProductId.Value);

        var total = await query.CountAsync();
        var data = await query
            .OrderByDescending(od => od.CreatedAt)
            .ThenBy(od => od.Id)
            .Skip((q.Page - 1) * q.Limit)
            .Take(q.Limit)
            .Select(ToDto)
            .ToListAsync();
        var totalPages = (int)Math.Ceiling(total / (double)q.Limit);

        return new PagedResponse<OrderDetailResponseDto>(
            data,
            new PaginationMeta(q.Page, q.Limit, total, totalPages));
    }
}
