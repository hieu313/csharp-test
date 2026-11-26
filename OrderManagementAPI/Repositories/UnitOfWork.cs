using Microsoft.EntityFrameworkCore.Storage;

namespace OrderManagement.Repositories;

public interface IUnitOfWork
{
    Task<ITransaction> BeginTransactionAsync();
    Task SaveChangesAsync();
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) => _db = db;

    public async Task<ITransaction> BeginTransactionAsync()
        => new EfTransaction(await _db.Database.BeginTransactionAsync());

    public Task SaveChangesAsync() => _db.SaveChangesAsync();

    private sealed class EfTransaction(IDbContextTransaction inner) : ITransaction
    {
        public Task CommitAsync() => inner.CommitAsync();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
