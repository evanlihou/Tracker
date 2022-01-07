using Bot;
using Microsoft.AspNetCore.Identity;
using Quartz;
using Tracker.Data;
using Tracker.Models;

namespace Tracker;

public class ProcessTelegramUpdatesJob : IJob
{
    private readonly ILogger<ProcessTelegramUpdatesJob> _logger;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TelegramBotService _bot;

    public ProcessTelegramUpdatesJob(ILogger<ProcessTelegramUpdatesJob> logger, ApplicationDbContext db, UserManager<ApplicationUser> userManager, TelegramBotService bot)
    {
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _bot = bot;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        await _bot.ProcessUpdates();
    }
}