﻿using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tracker.Models;

namespace Tracker.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions opt) : base(opt)
    {
        
    }

    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<OneTimeReminder> OneTimeReminders => Set<OneTimeReminder>();
    public DbSet<ReminderType> ReminderTypes => Set<ReminderType>();
    public DbSet<PersistentConfig> PersistentConfigs => Set<PersistentConfig>();
    public DbSet<ReminderCompletion> ReminderCompletions => Set<ReminderCompletion>();
    public DbSet<ReminderMessage> ReminderMessages => Set<ReminderMessage>();
    public DbSet<CompletionLink> CompletionLinks => Set<CompletionLink>();
    public DbSet<CountUp> CountUps => Set<CountUp>();
    public DbSet<CountUpHistory> CountUpHistories => Set<CountUpHistory>();
    
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // TODO: Save doesn't change anything to avoid breaking things
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.IsKeyless)
            {
                continue;
            }

            // TODO: I think this got easier to do since I wrote this monstrosity
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }
}