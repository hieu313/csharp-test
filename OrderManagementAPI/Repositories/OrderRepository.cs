using Microsoft.EntityFrameworkCore;

namespace OrderManagement.Repositories;

public interface IOrderRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task RecalculateTotalAsync(Guid orderId, CancellationToken ct = default);
}

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    // AnyAsync → SELECT CASE WHEN EXISTS(...): dừng ngay khi thấy 1 dòng, nhanh hơn Count > 0
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Orders.AnyAsync(o => o.Id == id, ct);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    // Tính lại từ đầu thay vì cộng/trừ dồn → không bao giờ lệch.
    // EF sinh COALESCE(SUM(...), 0) → order không còn detail thì total = 0.
    public Task RecalculateTotalAsync(Guid orderId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.Orders
            .Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.TotalAmount,
                    o => _db.OrderDetails.Where(d => d.OrderId == o.Id).Sum(d => d.Subtotal))
                .SetProperty(o => o.UpdatedAt, now), ct);
    }
}
