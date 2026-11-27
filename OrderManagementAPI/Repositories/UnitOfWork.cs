using Microsoft.EntityFrameworkCore.Storage;

namespace OrderManagement.Repositories;

public interface IUnitOfWork
{
    Task<ITransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) => _db = db;

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct = default)
        => new EfTransaction(await _db.Database.BeginTransactionAsync(ct));

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private sealed class EfTransaction(IDbContextTransaction inner) : ITransaction
    {
        public Task CommitAsync() => inner.CommitAsync();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
