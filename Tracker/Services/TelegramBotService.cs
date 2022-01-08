using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
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

    public async Task<bool> SendReminderToUser(long? userId, string message, int id)
    {
        if (userId == null) return false;

        var sentMessage = await _botClient.SendTextMessageAsync(new ChatId((long)userId), message,
            replyMarkup: new InlineKeyboardMarkup(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Done", $"done[{id}]"),
                    InlineKeyboardButton.WithCallbackData("Skip", $"skip[{id}]")
                }));

        _db.ReminderMessages.Add(new ReminderMessage
        {
            ReminderId = id,
            MessageId = sentMessage.MessageId
        });

        await _db.SaveChangesAsync();
        
        return true;
    }

    public async Task ProcessUpdates()
    {
        int? lastProcessedId = int.TryParse((await _configRepo.GetByCodeOrNull("TG_LAST_UPDATE_PROCESSED"))?.Value, out var f) ? f : default;
        var updates = await _botClient.GetUpdatesAsync(lastProcessedId);
        foreach (var update in updates)
        {
            if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery == null)
            {
                _logger.LogDebug("Skipped update that wasn't a callback query");
                continue;
            }
            
            if (update.CallbackQuery.Message?.From?.Id != _botClient.BotId)
            {
                _logger.LogWarning("Got a reply that didn't originate with the bot (messageId: {Message})", update.CallbackQuery.Message?.MessageId);
                continue;
            }
            
            _logger.LogInformation("Got callback with content '{CallbackContent}' from original message '{MessageContent}'", update.CallbackQuery.Data, update.CallbackQuery.Message?.Text);

            if (string.IsNullOrEmpty(update.CallbackQuery.Data))
            {
                continue;
            }

            var cbData = Regex.Match(update.CallbackQuery.Data, "([^\\[]+)\\[([\\d]+)\\]");
            var reminderId = cbData.Groups[2].Value;
            var action = cbData.Groups[1].Value;

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

            if (action == "done")
            {
                _db.ReminderCompletions.Add(new ReminderCompletion
                {
                    ReminderId = reminder.Id,
                    CompletionTime = DateTime.UtcNow
                });
            }

            var reminderMessages = _db.ReminderMessages.Where(x => x.ReminderId == reminder.Id);

            foreach (var message in reminderMessages)
            {
                await _botClient.DeleteMessageAsync(user.TelegramUserId!, message.MessageId);
            }
            
            _db.ReminderMessages.RemoveRange(reminderMessages);

            await _db.SaveChangesAsync();

            await SendMessageToUser(user.TelegramUserId, $"{reminder.Name} marked as {ActionToHumanReadable(action)}");
        }

        if (updates.Any())
            await _configRepo.UpdateByCode("TG_LAST_UPDATE_PROCESSED", (updates.Last().Id + 1).ToString());
    }

    private static string ActionToHumanReadable(string action)
    {
        return action switch
        {
            "done" => "completed",
            "skip" => "skipped",
            _ => throw new ApplicationException($"Unknown action {action}")
        };
    }
}