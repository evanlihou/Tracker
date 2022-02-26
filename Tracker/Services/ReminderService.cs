using Microsoft.AspNetCore.Identity;
using Quartz;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Services;

public class ReminderService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReminderService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Random _rng;

    public ReminderService(ApplicationDbContext db, ILogger<ReminderService> logger, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _logger = logger;
        _userManager = userManager;
        _rng = new Random();
    }
    
    public async Task<bool> MarkCompleted(int reminderId, int? nonce, bool isSkip, DateTime completionTime = default, CancellationToken cancellationToken = default)
    {
        var reminder = await _db.Reminders.FindAsync(new object?[] { reminderId }, cancellationToken: cancellationToken);

        if (reminder == null)
        {
            _logger.LogError("Unable to find reminder {ReminderId}", reminderId);
            return false;
        }

        // If nonces don't match and it's not the expected null value of 0
        if (reminder.Nonce != nonce && !(reminder.Nonce == null && nonce == 0))
        {
            _logger.LogWarning("Provided nonce {Provided} does not match expected {Expected}", nonce, reminder.Nonce);
            return false;
        }
        
        if (completionTime == default) completionTime = DateTime.UtcNow;

        reminder.LastRun = completionTime;
        
        if (!isSkip)
        {
            await _db.ReminderCompletions.AddAsync(new ReminderCompletion
            {
                ReminderId = reminderId,
                CompletionTime = completionTime
            }, cancellationToken);
        }

        // If <= 0, it was already set when the reminder was sent
        if (reminder.ReminderMinutes > 0)
        {
            var nextRun = await CalculateNextRunTime(reminder, completionTime, cancellationToken);
        
            reminder.NextRun = nextRun; 
        }

        reminder.Nonce = _rng.Next();

        _logger.LogInformation("Marked completion for reminder {Id}", reminderId);
        
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
    
    public async Task<DateTime?> CalculateNextRunTime(Reminder reminder, DateTime referenceTime = default, CancellationToken cancellationToken = default)
    {
        if (referenceTime == default) referenceTime = DateTime.UtcNow;
        
        var user = await _userManager.FindByIdAsync(reminder.UserId);

        var cronExpression = reminder.CronLocal != null
            ? new CronExpression(reminder.CronLocal)
            {
                TimeZone = user.TimeZone
            }
            : null;

        if (cronExpression == null) return null;

        if (reminder.StartDate != null && reminder.EndDate != null && reminder.StartDate > reminder.EndDate)
            return null;
        
        if (reminder.StartDate != null && reminder.StartDate > referenceTime)
        {
            return GetNthNextFireTime(1, (DateTime) reminder.StartDate, cronExpression);
        }

        if (reminder.EndDate != null && reminder.EndDate < referenceTime) return null;

        return GetNthNextFireTime(reminder.LastRun != null ? reminder.EveryNTriggers : 1, referenceTime, cronExpression);
    }

    private static DateTime? GetNthNextFireTime(int n, DateTime referenceTime, CronExpression cronExpression)
    {
        var nextRun = cronExpression.GetTimeAfter(referenceTime);
        for (var i = 0; i < n-1; i++)
        {
            if (nextRun == null) break;
            nextRun = cronExpression.GetTimeAfter((DateTimeOffset) nextRun);
        }

        return nextRun?.UtcDateTime;
    }
}