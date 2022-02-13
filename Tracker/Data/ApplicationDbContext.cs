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

    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ReminderType> ReminderTypes => Set<ReminderType>();
    public DbSet<PersistentConfig> PersistentConfigs => Set<PersistentConfig>();
    public DbSet<ReminderCompletion> ReminderCompletions => Set<ReminderCompletion>();
    public DbSet<ReminderMessage> ReminderMessages => Set<ReminderMessage>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}