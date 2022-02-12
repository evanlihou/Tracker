using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Tracker.Models;

namespace Tracker.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> opt) : base(opt)
    {
        
    }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<ReminderType> ReminderTypes { get; set; }
    public DbSet<PersistentConfig> PersistentConfigs { get; set; }
    public DbSet<ReminderCompletion> ReminderCompletions { get; set; }
    public DbSet<ReminderMessage> ReminderMessages { get; set; }
    
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
}