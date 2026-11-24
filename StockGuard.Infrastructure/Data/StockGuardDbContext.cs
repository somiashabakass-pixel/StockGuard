using Microsoft.EntityFrameworkCore;

namespace StockGuard.Infrastructure.Data;

public class StockGuardDbContext : DbContext
{
    public StockGuardDbContext(
        DbContextOptions<StockGuardDbContext> options)
        : base(options)
    {
    }
}