using Microsoft.EntityFrameworkCore;

namespace Tracker.Data;

public class SqliteApplicationDbContext : ApplicationDbContext
{
    public SqliteApplicationDbContext(DbContextOptions<SqliteApplicationDbContext> opt) : base(opt)
    {
    }
}