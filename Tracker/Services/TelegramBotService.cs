using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private readonly IWebHostEnvironment _environment;

    public TelegramBotService(TelegramSettings telegramSettings, ILogger<TelegramBotService> logger,
        ApplicationDbContext db, UserManager<ApplicationUser> userManager, PersistentConfigRepository configRepo,
        ReminderService reminderService, IWebHostEnvironment environment)
    {
        _botClient = new TelegramBotClient(telegramSettings.AccessToken);
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _configRepo = configRepo;
        _reminderService = reminderService;
        _environment = environment;
        _baseUrl = new Uri(telegramSettings.BaseUrl);
    }

    public async Task<User> GetBot()
    {
        return await _botClient.GetMeAsync();
    }

    private async Task<bool> SendMessageToUser(long? userId, string message)
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
        int? lastProcessedId =
            int.TryParse((await _configRepo.GetByCodeOrNull("TG_LAST_UPDATE_PROCESSED"))?.Value, out var f)
                ? f
                : default;
        var updates = await _botClient.GetUpdatesAsync(lastProcessedId);

        if (updates.Any())
            await _configRepo.UpdateByCode("TG_LAST_UPDATE_PROCESSED", (updates.Last().Id + 1).ToString());

        foreach (var update in updates)
        {
            if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text &&
                update.Message.Text != null)
            {
                if (update.Message.Text.StartsWith("/login"))
                {
                    await HandleLoginCommand(update.Message);
                }
                else if (update.Message.Text.StartsWith("/start"))
                {
                    await HandleStartCommand(update.Message);
                }
            }
            else
            {
                if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery == null)
                {
                    _logger.LogDebug("Skipped update that wasn't a callback query");
                    continue;
                }

                await HandleCallbackQuery(update);
            }
        }
    }

    private async Task<bool> HandleStartCommand(Message message)
    {
        var chatId = message.Chat.Id;
        var existingUser = _db.Users.SingleOrDefault(x => x.TelegramUserId == chatId);

        if (existingUser != null)
        {
            await _botClient.SendTextMessageAsync(chatId,
                "Bot started. You already have an account, send /login for a login link");
            return true;
        }

        var newUser = await _userManager.CreateAsync(new ApplicationUser()
        {
            UserName = $"tg@{chatId}",
            TelegramUserId = chatId
        });

        if (!newUser.Succeeded)
        {
            _logger.LogError("Failed to create new user: {Errors}",
                string.Join(", ", newUser.Errors.Select(x => x.Description)));
            await _botClient.SendTextMessageAsync(chatId, "There was a problem creating a user for your chat.");
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId,
                "Bot started. New account created, send /login for a login link");
        }

        return true;
    }

    private async Task<bool> HandleLoginCommand(Message message)
    {
        var chatId = message.Chat.Id;

        var sendingUser = await _db.Users.SingleOrDefaultAsync(x => x.TelegramUserId == chatId);
        if (sendingUser == null)
        {
            _logger.LogInformation("User not found to log in");
            return false;
        }

        var token = await _userManager.GenerateUserTokenAsync(sendingUser,
            PasswordlessLoginTokenProvider.Name, "telegram-token");
        if (token == null)
        {
            _logger.LogInformation("Could not generate token for user");
            return false;
        }

        var url = _baseUrl + "user/login?token=" + Uri.EscapeDataString(token) + "&id=" + sendingUser.Id;

        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Login URL {Url}", url);
        }
        
        await _botClient.SendTextMessageAsync(chatId,
            $"You can click the button below to log in to the website. The link will only be valid for 15 minutes.",
            replyToMessageId: message.MessageId,
            replyMarkup: new InlineKeyboardMarkup(
                new[]
                {
                    InlineKeyboardButton.WithUrl("Login", url),
                }));

        await _botClient.DeleteMessageAsync(chatId, message.MessageId);

        return true;
    }

    private async Task<bool> HandleCallbackQuery(Update update)
    {
        if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery == null)
        {
            _logger.LogWarning("Tried to process a callback query that didn't have any callback query data");
            return false;
        }

        if (update.CallbackQuery.Message?.From?.Id != _botClient.BotId)
        {
            _logger.LogWarning("Got a callback from a message that originate with the bot (messageId: {Message})",
                update.CallbackQuery.Message?.MessageId);
            return false;
        }

        _logger.LogInformation("Got callback with content '{CallbackContent}' from original message '{MessageContent}'",
            update.CallbackQuery.Data, update.CallbackQuery.Message?.Text);

        if (string.IsNullOrEmpty(update.CallbackQuery.Data))
        {
            return false;
        }

        // Example callback data: `done[1][n=1208346]` where 1 is reminderid and 1208346 is nonce
        var cbData = Regex.Match(update.CallbackQuery.Data,
            "(?'action'[^\\[]+)\\[(?'id'[\\d]+)\\](\\[n=(?'nonce'[\\d]+)\\])?");
        var reminderId = cbData.Groups["id"].Value;
        var action = cbData.Groups["action"].Value;
        var nonce = cbData.Groups["nonce"].Value;

        if (!int.TryParse(reminderId, out var parsedReminderId))
        {
            _logger.LogWarning("Failed to parse reminder ID {StrId} from callback data", reminderId);
            return false;
        }

        var parsedNonce = 0;
        if (!string.IsNullOrEmpty(nonce))
        {
            if (!int.TryParse(nonce, out parsedNonce)) _logger.LogWarning("Failed to parse nonce");
        }

        var reminder = await _db.Reminders.FindAsync(parsedReminderId);

        if (reminder == null)
        {
            _logger.LogWarning("Unable to find reminder {ReminderId}", parsedReminderId);
            return false;
        }

        var user = await _userManager.FindByIdAsync(reminder.UserId);

        if (user.TelegramUserId != update.CallbackQuery.Message?.Chat.Id)
        {
            _logger.LogWarning("Got a callback from the wrong chat. Expected {Expected} got {Actual}",
                user.TelegramUserId, update.CallbackQuery.Message?.Chat.Id);
        }

        var completionResult = await _reminderService.MarkCompleted(parsedReminderId, parsedNonce, action != "done");

        if (!completionResult)
        {
            _logger.LogWarning("Failed to mark completion for the reminder");
            return false;
        }
    
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

        return true;
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