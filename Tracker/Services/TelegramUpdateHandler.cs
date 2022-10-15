using System.Text.RegularExpressions;
using Bot;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Services;

public class TelegramUpdateHandler : IUpdateHandler
{
    private readonly ILogger<TelegramUpdateHandler> _logger;
    private readonly TelegramBotService _botService;
    private readonly TelegramBotClient _botClient;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Uri _baseUrl;
    private readonly IWebHostEnvironment _environment;
    private readonly PersistentConfigRepository _configRepo;
    private readonly ReminderService _reminderService;

    public TelegramUpdateHandler(ILogger<TelegramUpdateHandler> logger, TelegramBotService botService,
        TelegramBotClient botClient, ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        TelegramSettings telegramSettings, IWebHostEnvironment environment, PersistentConfigRepository configRepo,
        ReminderService reminderService)
    {
        _logger = logger;
        _botService = botService;
        _botClient = botClient;
        _db = db;
        _userManager = userManager;
        _environment = environment;
        _configRepo = configRepo;
        _reminderService = reminderService;
        _baseUrl = new Uri(telegramSettings.BaseUrl);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing update {}", update.Id);
        await _configRepo.UpdateByCode("TG_LAST_UPDATE_PROCESSED", (update.Id + 1).ToString(), cancellationToken);
        var handler = update switch
        {
            { Message: { } message } => OnMessageReceived(message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => OnCallbackQueryReceived(callbackQuery, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(update), update, null)
        };
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Polling for updates failed");
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is not { } messageText)
            return;

        if (message.Text.StartsWith("/login"))
            await HandleLoginCommand(message);
        else if (message.Text.StartsWith("/start")) await HandleStartCommand(message);

        async Task HandleStartCommand(Message message)
        {
            var chatId = message.Chat.Id;
            var existingUser = _db.Users.SingleOrDefault(x => x.TelegramUserId == chatId);

            if (existingUser != null)
            {
                await _botClient.SendTextMessageAsync(chatId,
                    "Bot started. You already have an account, send /login for a login link",
                    cancellationToken: cancellationToken);
                return;
            }

            var newUser = await _userManager.CreateAsync(new ApplicationUser
            {
                UserName = $"tg@{chatId}",
                TelegramUserId = chatId
            });

            if (!newUser.Succeeded)
            {
                _logger.LogError("Failed to create new user: {Errors}",
                    string.Join(", ", newUser.Errors.Select(x => x.Description)));
                await _botClient.SendTextMessageAsync(chatId, "There was a problem creating a user for your chat.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId,
                    "Bot started. New account created, send /login for a login link",
                    cancellationToken: cancellationToken);
            }
        }

        async Task HandleLoginCommand(Message message)
        {
            var chatId = message.Chat.Id;

            var sendingUser = await _db.Users.SingleOrDefaultAsync(x => x.TelegramUserId == chatId, cancellationToken);
            if (sendingUser == null)
            {
                _logger.LogInformation("User not found to log in");
                return;
            }

            var token = await _userManager.GenerateUserTokenAsync(sendingUser,
                PasswordlessLoginTokenProvider.Name, "telegram-token");
            if (token == null)
            {
                _logger.LogInformation("Could not generate token for user");
                return;
            }

            var url = _baseUrl + "user/login?token=" + Uri.EscapeDataString(token) + "&id=" + sendingUser.Id;

            if (_environment.IsDevelopment()) _logger.LogInformation("Login URL {Url}", url);

            await _botClient.SendTextMessageAsync(chatId,
                "You can click the button below to log in to the website. The link will only be valid for 15 minutes.",
                replyToMessageId: message.MessageId,
                replyMarkup: new InlineKeyboardMarkup(
                    new[]
                    {
                        InlineKeyboardButton.WithUrl("Login", url)
                    }), cancellationToken: cancellationToken);

            await _botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
        }
    }

    private async Task OnCallbackQueryReceived(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message?.From?.Id != _botClient.BotId)
        {
            _logger.LogWarning("Got a callback from a message that originate with the bot (messageId: {Message})",
                callbackQuery.Message?.MessageId);
            return;
        }

        _logger.LogInformation("Got callback with content '{CallbackContent}' from original message '{MessageContent}'",
            callbackQuery.Data, callbackQuery.Message?.Text);

        if (string.IsNullOrEmpty(callbackQuery.Data)) return;

        // Example callback data: `done[1][n=1208346]` where 1 is reminderid and 1208346 is nonce
        var cbData = Regex.Match(callbackQuery.Data,
            "(?'action'[^\\[]+)\\[(?'id'[\\d]+)\\](\\[n=(?'nonce'[\\d]+)\\])?");
        var reminderId = cbData.Groups["id"].Value;
        var action = cbData.Groups["action"].Value;
        var nonce = cbData.Groups["nonce"].Value;

        if (!int.TryParse(reminderId, out var parsedReminderId))
        {
            _logger.LogWarning("Failed to parse reminder ID {StrId} from callback data", reminderId);
            return;
        }

        var parsedNonce = 0;
        if (!string.IsNullOrEmpty(nonce))
            if (!int.TryParse(nonce, out parsedNonce))
                _logger.LogWarning("Failed to parse nonce");

        var reminder = await _db.Reminders.FindAsync(parsedReminderId);

        if (reminder == null)
        {
            _logger.LogWarning("Unable to find reminder {ReminderId}", parsedReminderId);
            return;
        }

        var user = await _userManager.FindByIdAsync(reminder.UserId);

        if (user.TelegramUserId != callbackQuery.Message?.Chat.Id)
            _logger.LogWarning("Got a callback from the wrong chat. Expected {Expected} got {Actual}",
                user.TelegramUserId, callbackQuery.Message?.Chat.Id);

        var completionResult = await _reminderService.MarkCompleted(parsedReminderId, parsedNonce, action != "done");

        if (!completionResult)
        {
            _logger.LogWarning("Failed to mark completion for the reminder");
            return;
        }

        try
        {
            var reminderMessages = _db.ReminderMessages.Where(x => x.ReminderId == reminder.Id);

            List<Task> deletedMessageTasks = new();
            foreach (var message in reminderMessages)
                deletedMessageTasks.Add(_botClient.DeleteMessageAsync(user.TelegramUserId!, message.MessageId,
                    cancellationToken));

            if (deletedMessageTasks.Any()) await Task.WhenAll(deletedMessageTasks);

            _db.ReminderMessages.RemoveRange(reminderMessages);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message(s)");
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _botService.SendMessageToUser(user.TelegramUserId,
            $"{reminder.Name} marked as {TelegramBotService.ActionToHumanReadable(action)}", true);
    }
}