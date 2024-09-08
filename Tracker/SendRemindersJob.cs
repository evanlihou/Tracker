using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Tracker.Data;
using Tracker.Models;
using Tracker.Services;

namespace Tracker;

public class SendRemindersJob(
    ILogger<SendRemindersJob> logger,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    TelegramBotService bot,
    ReminderService reminderService)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        //_logger.LogInformation("Sending reminders...");
        // TODO: Make this cleaner. I don't need to copy-paste all of this code for recurring vs one time reminders
        // Send recurring reminders
        var scheduledTime = context.ScheduledFireTimeUtc!.Value.UtcDateTime;
        var dueReminders = db.Reminders.Include(x => x.ReminderType).Where(x =>
            x.NextRun != null && x.NextRun <= scheduledTime).AsEnumerable();

        foreach (var reminder in dueReminders)
        {
            var user = await userManager.FindByIdAsync(reminder.UserId);
            if (user == null)
            {
                logger.LogError("User not found for reminder ID {Id}", reminder.Id);
                continue;
            }
            
            logger.LogInformation("Sending reminder to user {User} for reminder {Reminder}", user.Id, reminder.Id);

            await bot.SendReminderToUser(user.TelegramUserId, reminder.IsActionable, $"Reminder: {reminder.ReminderType!.Name} - {reminder.Name}", reminder.Id, reminder.Nonce ?? 0);

            reminder.IsPendingCompletion = true;
            if (reminder.ReminderMinutes <= 0)
            {
                reminder.LastRun = scheduledTime;
                reminder.NextRun = await reminderService.CalculateNextRunTime(reminder);
            }
            else
            {
                reminder.NextRun = scheduledTime.AddMinutes(reminder.ReminderMinutes);
            }
        }
        
        var dueOneTimeReminders = db.OneTimeReminders.Where(x =>
            x.NextRun != null && x.NextRun <= scheduledTime).AsEnumerable();

        foreach (var reminder in dueOneTimeReminders)
        {
            var user = await userManager.FindByIdAsync(reminder.UserId);
            if (user == null)
            {
                logger.LogError("User not found for reminder ID {Id}", reminder.Id);
                continue;
            }
            
            logger.LogInformation("Sending reminder to user {User} for reminder {Reminder}", user.Id, reminder.Id);

            await bot.SendReminderToUser(user.TelegramUserId, false, reminder.ToString(), reminder.Id, reminder.Nonce ?? 0);

            db.OneTimeReminders.Remove(reminder);
            // if (reminder.ReminderMinutes <= 0)
            // {
            //     reminder.LastRun = scheduledTime;
            //     reminder.NextRun = await _reminderService.CalculateNextRunTime(reminder);
            // }
            // else
            // {
            //     reminder.NextRun = scheduledTime.AddMinutes(reminder.ReminderMinutes);
            // }
        }

        await db.SaveChangesAsync();
    }
}