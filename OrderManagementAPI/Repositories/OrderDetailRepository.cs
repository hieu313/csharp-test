using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Dtos;

namespace OrderManagement.Repositories;

//trả thẳng DTO, tránh load cả entity vô ích, còn phải map lại ở service
public interface IOrderDetailRepository
{
    Task<OrderDetailResponseDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDetailResponseDto>> GetDtosByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<(IReadOnlyList<OrderDetailResponseDto> Items, int Total)> GetPagedDtosAsync(
        Guid? orderId, Guid? productId, int page, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDetailResponseDto>> GetDtosByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDetailResponseDto>> GetDtosByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<ProductSalesSummary> GetSalesSummaryByProductAsync(Guid productId, CancellationToken ct = default);

    //gọi trong transaction + khoá row
    Task<OrderDetail?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    void Add(OrderDetail detail);
    void AddRange(IEnumerable<OrderDetail> details);
    void Remove(OrderDetail detail);
}

public class OrderDetailRepository : IOrderDetailRepository
{
    private readonly AppDbContext _db;
    public OrderDetailRepository(AppDbContext db) => _db = db;

    // EF dịch Expression này sang SQL (JOIN Products lấy name/sku), không tải cả entity.
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

    public Task<OrderDetailResponseDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default)
        => _db.OrderDetails.AsNoTracking()
            .Where(od => od.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);

    // 1 query cho nhiều id, trả về đúng thứ tự ids truyền vào
    public async Task<IReadOnlyList<OrderDetailResponseDto>> GetDtosByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var byId = await _db.OrderDetails.AsNoTracking()
            .Where(od => ids.Contains(od.Id))
            .Select(ToDto)
            .ToDictionaryAsync(d => d.Id, ct);
        return ids.Select(id => byId[id]).ToList();
    }

    public async Task<(IReadOnlyList<OrderDetailResponseDto> Items, int Total)> GetPagedDtosAsync(
        Guid? orderId, Guid? productId, int page, int limit, CancellationToken ct = default)
    {
        var query = _db.OrderDetails.AsNoTracking();

        if (orderId.HasValue)
            query = query.Where(od => od.OrderId == orderId.Value);
        if (productId.HasValue)
            query = query.Where(od => od.ProductId == productId.Value);

        // Đếm sau khi lọc, trước khi phân trang
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(od => od.CreatedAt)
            .ThenBy(od => od.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(ToDto)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<OrderDetailResponseDto>> GetDtosByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _db.OrderDetails.AsNoTracking()
            .Where(od => od.OrderId == orderId)
            .OrderBy(od => od.CreatedAt)
            .ThenBy(od => od.Id)
            .Select(ToDto)
            .ToListAsync(ct);

    // Toàn bộ lịch sử, kể cả đơn đã hủy — mới nhất lên đầu
    public async Task<IReadOnlyList<OrderDetailResponseDto>> GetDtosByProductIdAsync(Guid productId, CancellationToken ct = default)
        => await _db.OrderDetails.AsNoTracking()
            .Where(od => od.ProductId == productId)
            .OrderByDescending(od => od.CreatedAt)
            .ThenBy(od => od.Id)
            .Select(ToDto)
            .ToListAsync(ct);

    // SELECT COUNT(DISTINCT order_id), SUM(quantity), SUM(subtotal)
    //   FROM OrderDetails JOIN Orders ... WHERE product_id = @id AND status <> 'cancelled'
    // GroupBy(_ => 1): lấy nhiều aggregate trong 1 query (thay vì 3 lần Count/Sum).
    public async Task<ProductSalesSummary> GetSalesSummaryByProductAsync(Guid productId, CancellationToken ct = default)
        => await _db.OrderDetails
            .Where(od => od.ProductId == productId && od.Order.Status != OrderStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new ProductSalesSummary(
                g.Select(od => od.OrderId).Distinct().Count(),   // distinct để tránh trùng theo order_id
                g.Sum(od => od.Quantity),
                g.Sum(od => od.Subtotal)))
            .FirstOrDefaultAsync(ct)
           ?? new ProductSalesSummary(0, 0, 0);                  // chưa bán lần nào → 0

    // Update/Delete phải đọc quantity cũ để quyết định cộng/trừ stock (đọc dòng A → ghi dòng B),
    // nên khóa để 2 request cùng lúc không cùng đọc giá trị cũ.
    public Task<OrderDetail?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
        => _db.OrderDetails
            .FromSqlInterpolated($"SELECT * FROM OrderDetails WITH (UPDLOCK, ROWLOCK) WHERE id = {id}")
            .FirstOrDefaultAsync(ct);

    public void Add(OrderDetail detail) => _db.OrderDetails.Add(detail);
    public void AddRange(IEnumerable<OrderDetail> details) => _db.OrderDetails.AddRange(details);
    public void Remove(OrderDetail detail) => _db.OrderDetails.Remove(detail);
}
