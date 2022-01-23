using Microsoft.AspNetCore.Identity;
using Quartz;
using Tracker.Data;
using Tracker.Models;

namespace Bot;

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

        if (!isSkip)
        {
            if (completionTime == default) completionTime = DateTime.UtcNow;
            
            await _db.ReminderCompletions.AddAsync(new ReminderCompletion
            {
                ReminderId = reminderId,
                CompletionTime = completionTime
            }, cancellationToken);
        }
        
        if (reminder?.CronLocal == null) return false;

        var user = await _userManager.FindByIdAsync(reminder.UserId);

        var cronExpression = new CronExpression(reminder.CronLocal)
        {
            TimeZone = user.TimeZone
        };
        
        var nextRun = cronExpression.GetTimeAfter(completionTime);
        
        if (nextRun != null)
            reminder.NextRun = ((DateTimeOffset)nextRun).UtcDateTime;

        reminder.Nonce = _rng.Next();

        _logger.LogInformation("Marked completion for reminder {Id}", reminderId);
        
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}