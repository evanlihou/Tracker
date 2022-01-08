using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Duende.IdentityServer.EntityFramework.Options;
using Tracker.Models;

namespace Tracker.Data;

public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions options, IOptions<OperationalStoreOptions> operationalStoreOptions)
        : base(options, operationalStoreOptions)
    {
    }

    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<ReminderType> ReminderTypes { get; set; }
    public DbSet<PersistentConfig> PersistentConfigs { get; set; }
    public DbSet<ReminderCompletion> ReminderCompletions { get; set; }
    public DbSet<ReminderMessage> ReminderMessages { get; set; }
}