using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Types;
using Tracker.Data;
using Tracker.Models;

namespace Bot;

public class TelegramSettings
{
    public string AccessToken { get; init; } = "";
}
public class TelegramBotService
{
    private readonly TelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly PersistentConfigRepository _configRepo;
    
    public TelegramBotService(TelegramSettings telegramSettings, ILogger<TelegramBotService> logger, ApplicationDbContext db, UserManager<ApplicationUser> userManager, PersistentConfigRepository configRepo)
    {
        _botClient = new TelegramBotClient(telegramSettings.AccessToken);
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _configRepo = configRepo;
    }

    public async Task<User> GetBot()
    {
        return await _botClient.GetMeAsync();
    }

    public async Task<bool> SendMessageToUser(long? userId, string message)
    {
        if (userId == null) return false;
        
        await _botClient.SendTextMessageAsync(new ChatId((long)userId), message);
        return true;
    }

    public async Task ProcessUpdates()
    {
        int? lastProcessedId = int.TryParse((await _configRepo.GetByCodeOrNull("TG_LAST_UPDATE_PROCESSED"))?.Value, out var f) ? f : default;
        var updates = await _botClient.GetUpdatesAsync(lastProcessedId);
        foreach (var update in updates)
        {
            if (update.Message?.ReplyToMessage != null)
            {
                if (update.Message.ReplyToMessage.From?.Id != _botClient.BotId)
                {
                    _logger.LogWarning("Got a reply that didn't originate with the bot {Message}", update.Message.Text);
                    continue;
                }
                _logger.LogInformation("Got reply with content '{MessageContent}' from original message '{ReplyContent}'", update.Message.Text, update.Message.ReplyToMessage.Text);

                if (update.Message.ReplyToMessage.Text == null)
                {
                    continue;
                }
                
                var reminderId = Regex.Match(update.Message.ReplyToMessage.Text, "\\(\\(([\\d]+)\\)\\)").Groups[1].Value;

                if (!int.TryParse(reminderId, out var parsedReminderId)) continue;
                
                var reminder = await _db.Reminders.FindAsync(parsedReminderId);
                
                if (reminder?.CronLocal == null) continue;

                var user = await _userManager.FindByIdAsync(reminder.UserId);

                var cronExpression = new CronExpression(reminder.CronLocal)
                {
                    TimeZone = user.TimeZone
                };
            
                var nextRun = cronExpression.GetTimeAfter(DateTimeOffset.UtcNow);
            
                if (nextRun != null)
                    reminder.NextRun = ((DateTimeOffset)nextRun).UtcDateTime;

                _db.ReminderCompletions.Add(new ReminderCompletion
                {
                    ReminderId = reminder.Id,
                    CompletionTime = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
            }
        }

        if (updates.Any())
            await _configRepo.UpdateByCode("TG_LAST_UPDATE_PROCESSED", (updates.Last().Id + 1).ToString());
    }
}