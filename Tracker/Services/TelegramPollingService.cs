using Bot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Tracker.Data;

namespace Tracker.Services;

// A background service consuming a scoped service.
// See more: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services#consuming-a-scoped-service-in-a-background-task
/// <summary>
/// An abstract class to compose Polling background service and Receiver implementation classes
/// </summary>
public class TelegramPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly TelegramBotClient _botClient;

    public TelegramPollingService(
        IServiceProvider serviceProvider,
        ILogger<TelegramPollingService> logger, TelegramBotClient botClient)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _botClient = botClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting polling service");

        await DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        // Make sure we receive updates until Cancellation Requested,
        // no matter what errors our ReceiveAsync get
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            try
            {
                var configRepo = scope.ServiceProvider.GetRequiredService<PersistentConfigRepository>();
                int? lastProcessedId =
                    int.TryParse((await configRepo.GetByCodeOrNull("TG_LAST_UPDATE_PROCESSED", stoppingToken))?.Value, out var f)
                        ? f
                        : default;
                
                var receiverOptions = new ReceiverOptions()
                {
                    AllowedUpdates = Array.Empty<UpdateType>(),
                    ThrowPendingUpdates = lastProcessedId is null,
                    Offset = lastProcessedId + 1
                };
                var me = await _botClient.GetMeAsync(stoppingToken);
                _logger.LogInformation("Start receiving updates for {BotName}", me.Username ?? "My Awesome Bot");

                var updateHandler = scope.ServiceProvider.GetRequiredService<IUpdateHandler>();
                // Start receiving updates
                await _botClient.ReceiveAsync(
                    updateHandler: updateHandler,
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken);
            }
            // Update Handler only captures exception inside update polling loop
            // We'll catch all other exceptions here
            // see: https://github.com/TelegramBots/Telegram.Bot/issues/1106
            catch (Exception ex)
            {
                _logger.LogError("Polling failed with exception: {Exception}", ex);

                // Cooldown if something goes wrong
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}