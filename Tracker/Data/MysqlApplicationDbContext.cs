using Microsoft.EntityFrameworkCore;

namespace Tracker.Data;

public class MysqlApplicationDbContext : ApplicationDbContext
{
    public MysqlApplicationDbContext(DbContextOptions<MysqlApplicationDbContext> opt) : base(opt)
    {
    }
}