using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Tracker.Data;
using Tracker.Models;

namespace Tracker.Services;

public class TelegramSettings
{
    public string AccessToken { get; init; } = "";
    public string BaseUrl { get; init; } = "";
}

public class TelegramBotService
{
    private readonly TelegramBotClient _botClient;
    private readonly ApplicationDbContext _db;

    public TelegramBotService(TelegramBotClient botClient, TelegramSettings telegramSettings,
        ApplicationDbContext db)
    {
        _botClient = botClient;
        _db = db;
    }

    public async Task<User> GetBot()
    {
        return await _botClient.GetMeAsync();
    }

    public async Task<bool> SendMessageToUser(long? userId, string message, bool silent = false)
    {
        if (userId == null) return false;

        await _botClient.SendTextMessageAsync(new ChatId((long)userId), message, disableNotification: silent);
        return true;
    }

    public async Task<bool> SendReminderToUser(long? userId, bool isActionable, string message, int id, int nonce)
    {
        if (userId == null) return false;

        var sentMessage = await _botClient.SendTextMessageAsync(new ChatId((long)userId), message,
            replyMarkup: isActionable ? new InlineKeyboardMarkup(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Done", $"done[{id}][n={nonce}]"),
                    InlineKeyboardButton.WithCallbackData("Skip", $"skip[{id}][n={nonce}]")
                }) : null);

        if (isActionable)
            _db.ReminderMessages.Add(new ReminderMessage
            {
                ReminderId = id,
                MessageId = sentMessage.MessageId
            });

        await _db.SaveChangesAsync();

        return true;
    }

    public static string ActionToHumanReadable(string action)
    {
        return action switch
        {
            "done" => "completed",
            "skip" => "skipped",
            _ => throw new ApplicationException($"Unknown action {action}")
        };
    }
}