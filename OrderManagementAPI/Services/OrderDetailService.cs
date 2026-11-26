using Microsoft.EntityFrameworkCore;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace OrderManagement.Services;

public interface IOrderDetailService
{
    Task<OrderDetailResponseDto?> GetByIdAsync(Guid id);
    Task<PagedResponse<OrderDetailResponseDto>> GetListAsync(OrderDetailQuery q);
    Task<OrderDetailResponseDto> CreateAsync(CreateOrderDetailRequest req);
    Task<IReadOnlyList<OrderDetailResponseDto>> CreateBulkAsync(CreateBulkOrderDetailRequest req);
    Task<OrderDetailResponseDto> UpdateAsync(Guid id, UpdateOrderDetailRequest req);
    Task DeleteAsync(Guid id);
    Task<OrderDetailsByOrderResponse> GetByOrderIdAsync(Guid orderId);
    Task<OrderDetailsByProductResponse> GetByProductIdAsync(Guid productId);
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

    public async Task<OrderDetailsByOrderResponse> GetByOrderIdAsync(Guid orderId)
    {
        // anyasync như case where exists, nhanh hơn count > 0
        var orderExists = await _db.Orders.AnyAsync(o => o.Id == orderId);
        if (!orderExists)
            throw new NotFoundException($"Order '{orderId}' not found");

        // lấy hết
        var data = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.OrderId == orderId)
            .OrderBy(od => od.CreatedAt)
            .ThenBy(od => od.Id)
            .Select(ToDto)
            .ToListAsync();

        var summary = new OrderSummary(
            TotalItems: data.Count,                      
            TotalQuantity: data.Sum(d => d.Quantity), 
            TotalAmount: data.Sum(d => d.Subtotal)); 

        return new OrderDetailsByOrderResponse(data, summary);
    }

    public async Task<OrderDetailsByProductResponse> GetByProductIdAsync(Guid productId)
    {
        var productExists = await _db.Products.AnyAsync(p => p.Id == productId);
        if (!productExists)
            throw new NotFoundException($"Product '{productId}' not found");

        //toàn bộ lịch sử
        var data = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => od.ProductId == productId)
            .OrderByDescending(od => od.CreatedAt)
            .ThenBy(od => od.Id)
            .Select(ToDto)
            .ToListAsync();

        // SELECT COUNT(DISTINCT order_id), SUM(quantity), SUM(subtotal)
        //   FROM OrderDetails JOIN Orders ... WHERE product_id = @id AND status <> 'cancelled'
        // GROUP BY (SELECT 1); 
        var summary = await _db.OrderDetails
            .Where(od => od.ProductId == productId && od.Order.Status != OrderStatus.Cancelled)
            .GroupBy(_ => 1) //(lấy nhiều aggregate trong 1 query (thay vì 3 lần Count/Sum).)
            .Select(g => new ProductSalesSummary(
                g.Select(od => od.OrderId).Distinct().Count(),   //distinct để tránh trùng theo order_id
                g.Sum(od => od.Quantity),
                g.Sum(od => od.Subtotal)))
            .FirstOrDefaultAsync()
            ?? new ProductSalesSummary(0, 0, 0); //nếu chưa bán lần nào, trả về 0

        return new OrderDetailsByProductResponse(data, summary);
    }

    public async Task<OrderDetailResponseDto> CreateAsync(CreateOrderDetailRequest req)
    {
        var orderId = req.OrderId!.Value;
        var productId = req.ProductId!.Value;
        var quantity = req.Quantity!.Value;
        var discount = req.Discount ?? 0;

        await using var tx = await _db.Database.BeginTransactionAsync();

        await EnsureOrderEditableAsync(orderId, "add items");

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException($"Product '{productId}' not found");
        var unitPrice = product.Price;   // chốt giá tại thời điểm đặt

        EnsureDiscountValid(discount, unitPrice, quantity);
        await DecreaseStockAsync(productId, quantity);

        var detail = new OrderDetail(orderId, productId, quantity, unitPrice, discount, req.Notes);
        _db.OrderDetails.Add(detail);
        await _db.SaveChangesAsync();

        await RecalculateOrderTotalAsync(orderId);
        await tx.CommitAsync();

        return (await GetByIdAsync(detail.Id))!;
    }

    public async Task<IReadOnlyList<OrderDetailResponseDto>> CreateBulkAsync(CreateBulkOrderDetailRequest req)
    {
        var orderId = req.OrderId!.Value;
        var items = req.Items;

        var duplicates = items
            .GroupBy(i => i.ProductId!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new BadRequestException($"Duplicate products in request: {string.Join(", ", duplicates)}");

        await using var tx = await _db.Database.BeginTransactionAsync();

        await EnsureOrderEditableAsync(orderId, "add items");

// khoá tất cả product cần dùng (UPDLOCK) để tránh 2 request cùng lúc
        var productIds = items.Select(i => i.ProductId!.Value).ToList();
        var products = await _db.Products
            .FromSql($"SELECT * FROM Products WITH (UPDLOCK, ROWLOCK)")
            .Where(p => productIds.Contains(p.Id))
            .OrderBy(p => p.Id)
            .ToDictionaryAsync(p => p.Id);

        var errors = new List<BulkItemError>();
        for (var i = 0; i < items.Count; i++)
        {
            var productId = items[i].ProductId!.Value;
            var quantity = items[i].Quantity!.Value;
            var discount = items[i].Discount ?? 0;

            if (!products.TryGetValue(productId, out var product))
                errors.Add(new BulkItemError(i, productId, "Product not found"));
            else if (quantity > product.Stock)
                errors.Add(new BulkItemError(i, productId,
                    $"Insufficient stock: requested {quantity}, available {product.Stock}"));
            else if (discount > product.Price * quantity)
                errors.Add(new BulkItemError(i, productId, "Discount cannot exceed unitPrice × quantity"));
        }

        if (errors.Count > 0)
            throw new BadRequestException($"{errors.Count} of {items.Count} items are invalid, nothing was created")
            {
                Errors = errors
            };

        var details = new List<OrderDetail>(items.Count);
        foreach (var item in items)
        {
            var product = products[item.ProductId!.Value];
            var quantity = item.Quantity!.Value;

            product.DecreaseStock(quantity);
            details.Add(new OrderDetail(orderId, product.Id, quantity, product.Price, item.Discount ?? 0));
        }
        _db.OrderDetails.AddRange(details);
        await _db.SaveChangesAsync();

        await RecalculateOrderTotalAsync(orderId);

        await tx.CommitAsync();

        var createdIds = details.Select(d => d.Id).ToList();
        var created = await _db.OrderDetails
            .AsNoTracking()
            .Where(od => createdIds.Contains(od.Id))
            .Select(ToDto)
            .ToDictionaryAsync(d => d.Id);

        return createdIds.Select(id => created[id]).ToList();
    }

    public async Task<OrderDetailResponseDto> UpdateAsync(Guid id, UpdateOrderDetailRequest req)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var detail = await LockDetailAsync(id);
        await EnsureOrderEditableAsync(detail.OrderId, "modify items");

        var newQuantity = req.Quantity ?? detail.Quantity;
        var newDiscount = req.Discount ?? detail.Discount;

        // unitPrice giữ nguyên giá lúc đặt, không lấy lại giá hiện tại của Product
        EnsureDiscountValid(newDiscount, detail.UnitPrice, newQuantity);

        //Chỉ điều chỉnh stock theo phần chênh lệch
        var delta = newQuantity - detail.Quantity;
        if (delta > 0)
            await DecreaseStockAsync(detail.ProductId, delta);// lấy thêm hàng
        else if (delta < 0)
            await IncreaseStockAsync(detail.ProductId, -delta);// hoàn trả hàng

        detail.Update(req.Quantity, req.Discount, req.Notes);
        await _db.SaveChangesAsync();

        await RecalculateOrderTotalAsync(detail.OrderId);
        await tx.CommitAsync();

        return (await GetByIdAsync(id))!;
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var detail = await LockDetailAsync(id);
        await EnsureOrderEditableAsync(detail.OrderId, "remove items");

        // hoàn trả stock
        await IncreaseStockAsync(detail.ProductId, detail.Quantity);   

        _db.OrderDetails.Remove(detail);
        await _db.SaveChangesAsync();

        // Xóa detail duy nhất → SUM rỗng → total = 0
        await RecalculateOrderTotalAsync(detail.OrderId);
        await tx.CommitAsync();
    }

    // Đọc detail kèm UPDLOCK, giữ khóa đến hết transaction.
    // Update/Delete phải đọc quantity cũ để quyết định cộng/trừ stock (đọc dòng A → ghi dòng B),
    // nên cần khóa để 2 request cùng lúc không cùng đọc giá trị cũ:
    //   - Update: delta tính sai → stock lệch
    //   - Delete: cả 2 cùng hoàn stock → stock bị cộng 2 lần
    // Phải gọi bên trong transaction. Trả về entity được tracking (để Update/Remove).
    private async Task<OrderDetail> LockDetailAsync(Guid id)
    {
        return await _db.OrderDetails
            .FromSqlInterpolated($"SELECT * FROM OrderDetails WITH (UPDLOCK, ROWLOCK) WHERE id = {id}")
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Order detail '{id}' not found");
    }

    // Order phải tồn tại và ở trạng thái cho phép sửa (xem Order.IsEditable)
    private async Task EnsureOrderEditableAsync(Guid orderId, string action)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new NotFoundException($"Order '{orderId}' not found");

        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot {action}");
    }

    // mục 4, discount
    private static void EnsureDiscountValid(decimal discount, decimal unitPrice, int quantity)
    {
        if (discount > unitPrice * quantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");
    }

    // Atomic: UPDATE Products SET stock = stock - @qty WHERE id = @id AND stock >= @qty
    private async Task DecreaseStockAsync(Guid productId, int quantity)
    {
        var now = DateTime.UtcNow;
        var affected = await _db.Products
            .Where(p => p.Id == productId && p.Stock >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock - quantity)
                .SetProperty(p => p.UpdatedAt, now));

        if (affected == 0)
        {
            var available = await _db.Products
                .Where(p => p.Id == productId)
                .Select(p => (int?)p.Stock)
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException($"Product '{productId}' not found");
            throw new ConflictException($"Insufficient stock: requested {quantity}, available {available}");
        }
    }

    private async Task IncreaseStockAsync(Guid productId, int quantity)
    {
        var now = DateTime.UtcNow;
        await _db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock + quantity)
                .SetProperty(p => p.UpdatedAt, now));
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
