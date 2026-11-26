using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;

namespace OrderManagement.Services;

public interface IOrderDetailService
{
    Task<OrderDetailResponseDto?> GetByIdAsync(Guid id);
    Task<PagedResponse<OrderDetailResponseDto>> GetListAsync(OrderDetailQuery q);
    Task<OrderDetailResponseDto> CreateAsync(CreateOrderDetailRequest req);
    Task<OrderDetailResponseDto> UpdateAsync(Guid id, UpdateOrderDetailRequest req);
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

    public async Task<OrderDetailResponseDto> CreateAsync(CreateOrderDetailRequest req)
    {
        var orderId = req.OrderId!.Value;
        var productId = req.ProductId!.Value;
        var quantity = req.Quantity!.Value;
        var discount = req.Discount ?? 0;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new NotFoundException($"Order '{orderId}' not found");
        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException($"Product '{productId}' not found");
        var unitPrice = product.Price;

        if (discount > unitPrice * quantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

        //UPDATE Products SET stock = stock - @qty WHERE id = @id AND stock >= @qty
        var now = DateTime.UtcNow;
        var affected = await _db.Products
            .Where(p => p.Id == productId && p.Stock >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock - quantity)
                .SetProperty(p => p.UpdatedAt, now));
        if (affected == 0)
            throw new ConflictException($"Insufficient stock: requested {quantity}, available {product.Stock}");

        var detail = new OrderDetail(orderId, productId, quantity, unitPrice, discount, req.Notes);
        _db.OrderDetails.Add(detail);
        await _db.SaveChangesAsync();

        await RecalculateOrderTotalAsync(orderId);

        await tx.CommitAsync();

        return (await GetByIdAsync(detail.Id))!;
    }

    public async Task<OrderDetailResponseDto> UpdateAsync(Guid id, UpdateOrderDetailRequest req)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var detail = await _db.OrderDetails
            .FromSqlInterpolated($"SELECT * FROM OrderDetails WITH (UPDLOCK, ROWLOCK) WHERE id = {id}")
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Order detail '{id}' not found");

        var order = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == detail.OrderId);
        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot modify items");

        var newQuantity = req.Quantity ?? detail.Quantity;
        var newDiscount = req.Discount ?? detail.Discount;

        // unitPrice giữ nguyên giá lúc đặt (không lấy lại giá hiện tại của Product)
        if (newDiscount > detail.UnitPrice * newQuantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

        // delta > 0: lấy thêm hàng → trừ stock (có kiểm tra đủ hàng)
        // delta < 0: trả bớt hàng → cộng lại stock
        // delta = 0: chỉ đổi discount/notes → không đụng stock
        var delta = newQuantity - detail.Quantity;
        var now = DateTime.UtcNow;

        if (delta > 0)
        {
            var affected = await _db.Products
                .Where(p => p.Id == detail.ProductId && p.Stock >= delta)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Stock, p => p.Stock - delta)
                    .SetProperty(p => p.UpdatedAt, now));
            if (affected == 0)
            {
                var available = await _db.Products.Where(p => p.Id == detail.ProductId)
                    .Select(p => p.Stock).FirstAsync();
                throw new ConflictException($"Insufficient stock: need {delta} more, available {available}");
            }
        }
        else if (delta < 0)
        {
            await _db.Products
                .Where(p => p.Id == detail.ProductId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Stock, p => p.Stock - delta)   // delta âm → cộng vào
                    .SetProperty(p => p.UpdatedAt, now));
        }

        // Entity tự tính lại subtotal và updated_at
        detail.Update(req.Quantity, req.Discount, req.Notes);
        await _db.SaveChangesAsync();

        await RecalculateOrderTotalAsync(detail.OrderId);

        await tx.CommitAsync();

        return (await GetByIdAsync(id))!;
    }

    private Task RecalculateOrderTotalAsync(Guid orderId)
    {
        var now = DateTime.UtcNow;
        return _db.Orders
            .Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.TotalAmount,
                    o => _db.OrderDetails.Where(d => d.OrderId == o.Id).Sum(d => d.Subtotal))
                .SetProperty(o => o.UpdatedAt, now));
    }
}
