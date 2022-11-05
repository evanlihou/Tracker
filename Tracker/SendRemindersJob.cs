using Bot;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Tracker.Data;
using Tracker.Models;
using Tracker.Services;

namespace Tracker;

public class SendRemindersJob : IJob
{
    private readonly ILogger<SendRemindersJob> _logger;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TelegramBotService _bot;
    private readonly ReminderService _reminderService;

    public SendRemindersJob(ILogger<SendRemindersJob> logger, ApplicationDbContext db, UserManager<ApplicationUser> userManager, TelegramBotService bot, ReminderService reminderService)
    {
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _bot = bot;
        _reminderService = reminderService;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        //_logger.LogInformation("Sending reminders...");
        // TODO: Make this cleaner. I don't need to copy-paste all of this code for recurring vs one time reminders
        // Send recurring reminders
        var scheduledTime = context.ScheduledFireTimeUtc!.Value.UtcDateTime;
        var dueReminders = _db.Reminders.Include(x => x.ReminderType).Where(x =>
            x.NextRun != null && x.NextRun <= scheduledTime).AsEnumerable();

        foreach (var reminder in dueReminders)
        {
            var user = await _userManager.FindByIdAsync(reminder.UserId);
            if (user == null)
            {
                _logger.LogError("User not found for reminder ID {Id}", reminder.Id);
                continue;
            }
            
            _logger.LogInformation("Sending reminder to user {User} for reminder {Reminder}", user.Id, reminder.Id);

            await _bot.SendReminderToUser(user.TelegramUserId, reminder.IsActionable, $"Reminder: {reminder.ReminderType!.Name} - {reminder.Name}", reminder.Id, reminder.Nonce ?? 0);

            if (reminder.ReminderMinutes <= 0)
            {
                reminder.LastRun = scheduledTime;
                reminder.NextRun = await _reminderService.CalculateNextRunTime(reminder);
            }
            else
            {
                reminder.NextRun = scheduledTime.AddMinutes(reminder.ReminderMinutes);
            }
        }
        
        var dueOneTimeReminders = _db.OneTimeReminders.Where(x =>
            x.NextRun != null && x.NextRun <= scheduledTime).AsEnumerable();

        foreach (var reminder in dueOneTimeReminders)
        {
            var user = await _userManager.FindByIdAsync(reminder.UserId);
            if (user == null)
            {
                _logger.LogError("User not found for reminder ID {Id}", reminder.Id);
                continue;
            }
            
            _logger.LogInformation("Sending reminder to user {User} for reminder {Reminder}", user.Id, reminder.Id);

            await _bot.SendReminderToUser(user.TelegramUserId, false, reminder.ToString(), reminder.Id, reminder.Nonce ?? 0);

            _db.OneTimeReminders.Remove(reminder);
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

        await _db.SaveChangesAsync();
    }
}