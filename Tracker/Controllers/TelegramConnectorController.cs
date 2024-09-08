using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tracker.Models;
using Tracker.Services;

namespace Tracker.Controllers;

[Authorize]
[ApiController]
[Route("telegram")]
public class TelegramConnectorController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TelegramBotService _bot;

    public TelegramConnectorController(UserManager<ApplicationUser> userManager, TelegramBotService bot)
    {
        _userManager = userManager;
        _bot = bot;
    }

    

    [HttpGet("botInfo")]
    public async Task<object> GetBotInfo()
    {
        return await _bot.GetBot();
    }
}