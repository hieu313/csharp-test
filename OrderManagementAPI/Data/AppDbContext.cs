using Microsoft.EntityFrameworkCore;

namespace OrderManagement;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}