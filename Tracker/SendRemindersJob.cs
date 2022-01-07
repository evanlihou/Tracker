using Bot;
using Microsoft.AspNetCore.Identity;
using Quartz;
using Tracker.Data;
using Tracker.Models;

namespace Tracker;

public class SendRemindersJob : IJob
{
    private readonly ILogger<SendRemindersJob> _logger;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TelegramBotService _bot;

    public SendRemindersJob(ILogger<SendRemindersJob> logger, ApplicationDbContext db, UserManager<ApplicationUser> userManager, TelegramBotService bot)
    {
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _bot = bot;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        //_logger.LogInformation("Sending reminders...");

        var dueReminders = _db.Reminders.Where(x =>
            x.NextRun <= DateTime.UtcNow && (x.StartDate == null || x.StartDate <= DateTime.UtcNow) && (x.EndDate == null || x.EndDate >= DateTime.UtcNow)).AsEnumerable();

        foreach (var reminder in dueReminders)
        {
            var user = await _userManager.FindByIdAsync(reminder.UserId);
            if (user == null)
            {
                _logger.LogError("User not found for reminder ID {Id}", reminder.Id);
                continue;
            }
            
            _logger.LogInformation("Sending reminder to user {User} for reminder {Reminder}", user.Id, reminder.Id);

            await _bot.SendMessageToUser(user.TelegramUserId, $"Reminder: {reminder.Name} (({reminder.Id}))");
            /*
            if (reminder.CronLocal == null) continue;

            var cronExpression = new CronExpression(reminder.CronLocal)
            {
                TimeZone = user.TimeZone
            };
            
            var nextRun = cronExpression.GetTimeAfter(DateTimeOffset.UtcNow);
            
            if (nextRun != null)
                reminder.NextRun = ((DateTimeOffset)nextRun).UtcDateTime;
            */

            reminder.NextRun = DateTimeOffset.UtcNow.AddMinutes(reminder.ReminderMinutes).UtcDateTime;
        }

        await _db.SaveChangesAsync();
    }
}