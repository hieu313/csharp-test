using Microsoft.EntityFrameworkCore;

namespace OrderManagement.Repositories;

public interface IProductRepository
{
    Task<bool> ExistsAsync(Guid id);
    Task<Product?> GetByIdAsync(Guid id);
    Task<int?> GetStockAsync(Guid id);

    //trong transaction
    Task<Dictionary<Guid, Product>> GetByIdsForUpdateAsync(IReadOnlyCollection<Guid> ids);

    // Atomic: trừ nếu đủ hàng. false = không đủ hàng hoặc product không tồn tại.
    Task<bool> TryDecreaseStockAsync(Guid id, int quantity);
    Task IncreaseStockAsync(Guid id, int quantity);
}

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid id) => _db.Products.AnyAsync(p => p.Id == id);

    public Task<Product?> GetByIdAsync(Guid id)
        => _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public Task<int?> GetStockAsync(Guid id)
        => _db.Products.Where(p => p.Id == id).Select(p => (int?)p.Stock).FirstOrDefaultAsync();

    //khoá row đến hết transaction
    // Không AsNoTracking: cần EF theo dõi để SaveChanges ghi stock mới.
    public Task<Dictionary<Guid, Product>> GetByIdsForUpdateAsync(IReadOnlyCollection<Guid> ids)
        => _db.Products
            .FromSql($"SELECT * FROM Products WITH (UPDLOCK, ROWLOCK)")
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Id)
            .ToDictionaryAsync(p => p.Id);

    //UPDATE Products SET stock = stock - @qty WHERE id = @id AND stock >= @qty
    public async Task<bool> TryDecreaseStockAsync(Guid id, int quantity)
    {
        var now = DateTime.UtcNow;
        var affected = await _db.Products
            .Where(p => p.Id == id && p.Stock >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock - quantity)
                .SetProperty(p => p.UpdatedAt, now));
        return affected > 0;
    }

    public Task IncreaseStockAsync(Guid id, int quantity)
    {
        var now = DateTime.UtcNow;
        return _db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock + quantity)
                .SetProperty(p => p.UpdatedAt, now));
    }
}
