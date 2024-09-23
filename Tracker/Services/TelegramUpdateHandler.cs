using System.Text.RegularExpressions;
using Chronic.Core;
using Chronic.Core.Tags;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TimeZoneConverter;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Services;

public partial class TelegramUpdateHandler(
    ILogger<TelegramUpdateHandler> logger,
    TelegramBotService botService,
    TelegramBotClient botClient,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    TelegramSettings telegramSettings,
    IWebHostEnvironment environment,
    PersistentConfigRepository configRepo,
    ReminderService reminderService)
    : IUpdateHandler
{
    private readonly Uri _baseUrl = new(telegramSettings.BaseUrl);

    // We can only ever have one instance of the app listening to updates, so this should be safe
    private static readonly Dictionary<long, ReminderBuilder> ReminderBuilders = new();

    private class ReminderBuilder
    {
        public ReminderBuilderState State = ReminderBuilderState.AwaitingName;
        public string? Name;
        public enum ReminderBuilderState
        {
            AwaitingName,
            AwaitingTime
        }
    }

    public async Task HandleUpdateAsync(ITelegramBotClient _, Update update,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing update {}", update.Id);
        await configRepo.UpdateByCode("TG_LAST_UPDATE_PROCESSED", update.Id.ToString(), cancellationToken);
        var handler = update switch
        {
            { Message: { } message } => OnMessageReceived(message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => OnCallbackQueryReceived(callbackQuery, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(update), update, null)
        };
    }

    public Task HandleErrorAsync(ITelegramBotClient _, Exception exception,
        HandleErrorSource __, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Polling for updates failed");
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
            return;

        var chatId = message.Chat.Id;

        if (message.Text.StartsWith("/login")) await HandleLoginCommand();
        else if (message.Text.StartsWith("/start")) await HandleStartCommand();
        else if (message.Text.StartsWith("/remind")) await HandleRemindCommand();
        else if (message.Text.StartsWith("/cancel")) await HandleCancelCommand();
        else await HandleStatefulMessage();
        return;

        async Task HandleStartCommand()
        {
            var existingUser = db.Users.SingleOrDefault(x => x.TelegramUserId == chatId);

            if (existingUser != null)
            {
                await botClient.SendTextMessageAsync(chatId,
                    "Bot started. You already have an account, send /login for a login link",
                    cancellationToken: cancellationToken);
                return;
            }

            var newUser = await userManager.CreateAsync(new ApplicationUser
            {
                UserName = $"tg@{chatId}",
                TelegramUserId = chatId
            });

            if (!newUser.Succeeded)
            {
                logger.LogError("Failed to create new user: {Errors}",
                    string.Join(", ", newUser.Errors.Select(x => x.Description)));
                await botClient.SendTextMessageAsync(chatId, "There was a problem creating a user for your chat.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId,
                    "Bot started. New account created, send /login for a login link",
                    cancellationToken: cancellationToken);
            }
        }

        async Task HandleLoginCommand()
        {
            var sendingUser = await db.Users.SingleOrDefaultAsync(x => x.TelegramUserId == chatId, cancellationToken);
            if (sendingUser == null)
            {
                logger.LogInformation("User not found to log in");
                return;
            }

            var token = await userManager.GenerateUserTokenAsync(sendingUser,
                PasswordlessLoginTokenProvider.Name, "telegram-token");
            if (string.IsNullOrEmpty(token))
            {
                logger.LogInformation("Could not generate token for user");
                return;
            }

            var url = _baseUrl + "user/login?token=" + Uri.EscapeDataString(token) + "&id=" + sendingUser.Id;

            if (environment.IsDevelopment()) logger.LogInformation("Login URL {Url}", url);
            
            await botClient.SendTextMessageAsync(chatId,
                "You can click the button below to log in to the website. The link will only be valid for 15 minutes.",
                replyParameters: message.MessageId,
                replyMarkup: new InlineKeyboardMarkup(
                    new[]
                    {
                        InlineKeyboardButton.WithUrl("Login", url)
                    }), cancellationToken: cancellationToken);

            await botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
        }

        async Task HandleRemindCommand()
        {
            if (ReminderBuilders.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId,
                    "You're already creating a reminder. Send `/cancel` first or finish what you were doing.",
                    cancellationToken: cancellationToken);
                return;
            }

            ReminderBuilders[chatId] = new ReminderBuilder();
            await botClient.SendTextMessageAsync(chatId,
                "Ok, what should the name of the reminder be?",
                cancellationToken: cancellationToken);
        }

        async Task HandleStatefulMessage()
        {
            if (message.Chat.Type == ChatType.Private && !ReminderBuilders.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "I'm not sure what you mean",
                    cancellationToken: cancellationToken);
                return;
            }

            var reminderBuilder = ReminderBuilders[chatId];
            if (reminderBuilder.State == ReminderBuilder.ReminderBuilderState.AwaitingName)
            {
                reminderBuilder.Name = message.Text;
                reminderBuilder.State = ReminderBuilder.ReminderBuilderState.AwaitingTime;
                await botClient.SendTextMessageAsync(message.Chat.Id,
                    "When should you be reminded?", cancellationToken: cancellationToken);
            } else if (reminderBuilder.State == ReminderBuilder.ReminderBuilderState.AwaitingTime)
            {
                var user = await db.Users.SingleAsync(x => x.TelegramUserId == chatId,
                    cancellationToken: cancellationToken);
                var userTimeZone = TZConvert.GetTimeZoneInfo(user.TimeZoneId);
                
                var textDate = message.Text;
                var parser = new Parser(new Options()
                {
                    Context = Pointer.Type.Future,
                    Clock = () => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone)
                });
                var parsedDate = parser.Parse(textDate).Start;
                if (parsedDate is null)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id,
                        $"Failed to parse date '{textDate}'", cancellationToken: cancellationToken);
                    return;
                }

                var utcReminderTime = TimeZoneInfo.ConvertTimeToUtc(parsedDate.Value, userTimeZone);

                db.OneTimeReminders.Add(new OneTimeReminder()
                {
                    Name = reminderBuilder.Name!,
                    NextRun = utcReminderTime,
                    UserId = user.Id
                });

                await db.SaveChangesAsync(cancellationToken);
                
                await botClient.SendTextMessageAsync(message.Chat.Id,
                    $"Reminder saved", cancellationToken: cancellationToken);

                ReminderBuilders.Remove(chatId);
            }
            else
            {
                // Should never happen
                throw new ArgumentOutOfRangeException(nameof(reminderBuilder),
                    $"ReminderBuilder State {reminderBuilder.State} not implemented");
            }
        }

        async Task HandleCancelCommand()
        {
            if (ReminderBuilders.ContainsKey(chatId))
            {
                ReminderBuilders.Remove(chatId);
                await botClient.SendTextMessageAsync(chatId, "Reminder creation cancelled",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "No operation to cancel",
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task OnCallbackQueryReceived(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message?.From?.Id != botClient.BotId)
        {
            logger.LogWarning("Got a callback from a message that originate with the bot (messageId: {Message})",
                callbackQuery.Message?.MessageId);
            return;
        }

        logger.LogInformation("Got callback with content '{CallbackContent}' from original message '{MessageContent}'",
            callbackQuery.Data, callbackQuery.Message?.Text);

        if (string.IsNullOrEmpty(callbackQuery.Data)) return;

        // Example callback data: `done[1][n=1208346]` where 1 is reminderid and 1208346 is nonce
        var cbData = CallbackRegex().Match(callbackQuery.Data);
        var reminderId = cbData.Groups["id"].Value;
        var action = cbData.Groups["action"].Value;
        var nonce = cbData.Groups["nonce"].Value;

        if (!int.TryParse(reminderId, out var parsedReminderId))
        {
            logger.LogWarning("Failed to parse reminder ID {StrId} from callback data", reminderId);
            return;
        }

        var parsedNonce = 0;
        if (!string.IsNullOrEmpty(nonce))
            if (!int.TryParse(nonce, out parsedNonce))
                logger.LogWarning("Failed to parse nonce");

        var reminder = await db.Reminders.FindAsync([parsedReminderId], cancellationToken: cancellationToken);

        if (reminder == null)
        {
            logger.LogWarning("Unable to find reminder {ReminderId}", parsedReminderId);
            return;
        }

        var user = await userManager.FindByIdAsync(reminder.UserId);

        if (user is null)
        {
            logger.LogWarning("Could not find user for reminder {Id}", reminder.Id);
            return;
        }

        if (user.TelegramUserId != callbackQuery.Message?.Chat.Id)
            logger.LogWarning("Got a callback from the wrong chat. Expected {Expected} got {Actual}",
                user.TelegramUserId, callbackQuery.Message?.Chat.Id);

        var completionResult = await reminderService.MarkCompleted(parsedReminderId, parsedNonce, action != "done",
            cancellationToken: cancellationToken);

        if (!completionResult)
        {
            logger.LogWarning("Failed to mark completion for the reminder");
            return;
        }

        await db.SaveChangesAsync(cancellationToken);

        // TODO: Move this to ReminderService?
        await botService.SendMessageToUser(user.TelegramUserId,
            $"{reminder.Name} marked as {TelegramBotService.ActionToHumanReadable(action)}", true);
    }

    [GeneratedRegex(@"(?'action'[^\[]+)\[(?'id'[\d]+)\](\[n=(?'nonce'[\d]+)\])?")]
    private static partial Regex CallbackRegex();
}
