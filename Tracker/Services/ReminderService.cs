using Microsoft.AspNetCore.Identity;
using Quartz;
using Telegram.Bot;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Services;

public class ReminderService(
    ApplicationDbContext db,
    ILogger<ReminderService> logger,
    UserManager<ApplicationUser> userManager,
    TelegramBotClient botClient)
{
    private readonly Random _rng = new();

    public async Task<bool> MarkCompleted(int reminderId, int? nonce, bool isSkip, DateTime completionTime = default, CancellationToken cancellationToken = default)
    {
        var reminder = await db.Reminders.FindAsync(new object?[] { reminderId }, cancellationToken: cancellationToken);

        if (reminder == null)
        {
            logger.LogError("Unable to find reminder {ReminderId}", reminderId);
            return false;
        }

        var user = await db.Users.FindAsync(new object?[] { reminder.UserId }, cancellationToken);

        // If nonces don't match and it's not the expected null value of 0
        if (nonce is not null && (reminder.Nonce != nonce && !(reminder.Nonce == null && nonce == 0)))
        {
            logger.LogWarning("Provided nonce {Provided} does not match expected {Expected}", nonce, reminder.Nonce);
            return false;
        }
        
        if (completionTime == default) completionTime = DateTime.UtcNow;

        reminder.LastRun = completionTime;
        
        if (!isSkip)
        {
            await db.ReminderCompletions.AddAsync(new ReminderCompletion
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

        reminder.Nonce = _rng.Next(int.MaxValue);
        reminder.IsPendingCompletion = false;
        
        try
        {
            // Telegram can only handle deleting 100 messages at a time, so paginate the data
            var lastIdDeleted = 0;
            do
            {
                var messagesPage = db.ReminderMessages
                    .Where(x => x.ReminderId == reminder.Id && x.Id > lastIdDeleted).Take(100).OrderBy(x => x.Id);
                if (!messagesPage.Any()) break;
                await botClient.DeleteMessagesAsync(user!.TelegramUserId!, messagesPage.Select(m => m.MessageId),
                    cancellationToken: cancellationToken);
                lastIdDeleted = messagesPage.Last().Id;
                db.ReminderMessages.RemoveRange(messagesPage);
            } while (true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete message(s)");
        }
        logger.LogInformation("Marked completion for reminder {Id}", reminderId);
        
        await db.SaveChangesAsync(cancellationToken);
        
        return true;
    }
    
    public async Task<DateTime?> CalculateNextRunTime(Reminder reminder, DateTime referenceTime = default, CancellationToken cancellationToken = default)
    {
        if (referenceTime == default) referenceTime = DateTime.UtcNow;

        var user = await userManager.FindByIdAsync(reminder.UserId);

        var cronExpression = reminder.CronLocal != null
            ? new CronExpression(reminder.CronLocal)
            {
                TimeZone = user!.TimeZone
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
