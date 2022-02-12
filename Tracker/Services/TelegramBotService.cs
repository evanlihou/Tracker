using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Tracker.Data;
using Tracker.Models;
using Tracker.Services;

namespace Bot;

public class TelegramSettings
{
    public string AccessToken { get; init; } = "";
    public string BaseUrl { get; init; } = "";
}
public class TelegramBotService
{
    private readonly TelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ReminderService _reminderService;
    private readonly PersistentConfigRepository _configRepo;
    private readonly Uri _baseUrl;
    
    public TelegramBotService(TelegramSettings telegramSettings, ILogger<TelegramBotService> logger, ApplicationDbContext db, UserManager<ApplicationUser> userManager, PersistentConfigRepository configRepo, ReminderService reminderService)
    {
        _botClient = new TelegramBotClient(telegramSettings.AccessToken);
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _configRepo = configRepo;
        _reminderService = reminderService;
        _baseUrl = new Uri(telegramSettings.BaseUrl);
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

    public async Task<bool> SendReminderToUser(long? userId, string message, int id, int nonce)
    {
        if (userId == null) return false;

        var sentMessage = await _botClient.SendTextMessageAsync(new ChatId((long)userId), message,
            replyMarkup: new InlineKeyboardMarkup(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Done", $"done[{id}][n={nonce}]"),
                    InlineKeyboardButton.WithCallbackData("Skip", $"skip[{id}][n={nonce}]")
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
        
        if (updates.Any())
            await _configRepo.UpdateByCode("TG_LAST_UPDATE_PROCESSED", (updates.Last().Id + 1).ToString());
        
        foreach (var update in updates)
        {
            if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text && update.Message.Text != null)
            {
                if (update.Message.Text.StartsWith("/login"))
                {
                    var chatId = update.Message.Chat.Id;
                    var sendingUser = _db.Users.SingleOrDefault(x => x.TelegramUserId == chatId);

                    if (sendingUser == null) continue;

                    var token = await _userManager.GenerateUserTokenAsync(sendingUser,
                        PasswordlessLoginTokenProvider<ApplicationUser>.Name, "telegram-token");

                    if (token == null) continue;

                    var url = _baseUrl + "user/login?token=" + Uri.EscapeDataString(token) + "&id=" + sendingUser.Id;

                    await _botClient.SendTextMessageAsync(chatId,
                        $"You can click the button below to log in to the website. The link will only be valid for 15 minutes.",
                        replyToMessageId: update.Message.MessageId,
                        replyMarkup: new InlineKeyboardMarkup(
                            new[]
                            {
                                InlineKeyboardButton.WithUrl("Login", url),
                            }));
                }
                else if (update.Message.Text.StartsWith("/start"))
                {
                    var chatId = update.Message.Chat.Id;
                    var existingUser = _db.Users.SingleOrDefault(x => x.TelegramUserId == chatId);

                    if (existingUser != null)
                    {
                        await _botClient.SendTextMessageAsync(chatId,
                            "Bot started. You already have an account, send /login for a login link");
                        continue;
                    }

                    var newUser = await _userManager.CreateAsync(new ApplicationUser()
                    {
                        UserName = $"tg@{chatId}",
                        TelegramUserId = chatId
                    });

                    if (!newUser.Succeeded)
                    {
                        _logger.LogError("Failed to create new user: {Errors}", string.Join(", ", newUser.Errors.Select(x => x.Description)));
                        await _botClient.SendTextMessageAsync(chatId, "There was a problem creating a user for your chat.");
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(chatId,
                            "Bot started. New account created, send /login for a login link");
                    }
                }
            }
            else
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

                // Example callback data: `done[1][n=1208346]` where 1 is reminderid and 1208346 is nonce
                var cbData = Regex.Match(update.CallbackQuery.Data, "(?'action'[^\\[]+)\\[(?'id'[\\d]+)\\](\\[n=(?'nonce'[\\d]+)\\])?");
                var reminderId = cbData.Groups["id"].Value;
                var action = cbData.Groups["action"].Value;
                var nonce = cbData.Groups["nonce"].Value;

                if (!int.TryParse(reminderId, out var parsedReminderId)) continue;

                var parsedNonce = 0;
                if (!string.IsNullOrEmpty(nonce))
                {
                    if (!int.TryParse(nonce, out parsedNonce)) _logger.LogWarning("Failed to parse nonce");
                }
                
                var reminder = await _db.Reminders.FindAsync(parsedReminderId);

                if (reminder == null)
                {
                    _logger.LogWarning("Unable to find reminder {ReminderId}", parsedReminderId);
                    continue;
                }

                var user = await _userManager.FindByIdAsync(reminder.UserId);

                if (user.TelegramUserId != update.CallbackQuery.Message?.Chat.Id)
                {
                    _logger.LogWarning("Got a callback from the wrong person. Expected {Expected} got {Actual}", user.TelegramUserId, update.CallbackQuery.Message?.Chat.Id);
                }
                
                var completionResult = await _reminderService.MarkCompleted(parsedReminderId, parsedNonce, action != "done");

                if (!completionResult) continue;

                var reminderMessages = _db.ReminderMessages.Where(x => x.ReminderId == reminder.Id);

                List<Task> deletedMessageTasks = new();
                foreach (var message in reminderMessages)
                {
                    deletedMessageTasks.Add(_botClient.DeleteMessageAsync(user.TelegramUserId!, message.MessageId));
                }

                try
                {
                    if (deletedMessageTasks.Any()) await Task.WhenAll(deletedMessageTasks);
                }
                catch (AggregateException ex)
                {
                    _logger.LogWarning(ex, "Failed to delete message(s)");
                }

                _db.ReminderMessages.RemoveRange(reminderMessages);

                await _db.SaveChangesAsync();

                await SendMessageToUser(user.TelegramUserId, $"{reminder.Name} marked as {ActionToHumanReadable(action)}");
            }
        }
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