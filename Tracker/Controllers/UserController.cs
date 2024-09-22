using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot.Extensions.LoginWidget;
using TimeZoneConverter;
using Tracker.Models;
using Tracker.Services;

namespace Tracker.Controllers;

[Authorize]
[Route("user")]
public class UserController(SignInManager<ApplicationUser> signInManager, ILogger<UserController> logger)
    : BaseController
{
    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<ActionResult> Login([FromQuery] string token, [FromQuery] string id)
    {
        var user = await UserManager.FindByIdAsync(id);
        if (user == null) return BadRequest("User not found");

        var tokenValid = await UserManager.VerifyUserTokenAsync(user,
            PasswordlessLoginTokenProvider.Name, "telegram-token", token);

        if (!tokenValid) return BadRequest("Invalid login token");
        
        await UserManager.UpdateSecurityStampAsync(user);
        await signInManager.SignInAsync(user, false, "passwordless-token");
        
        return RedirectToAction("Index", "Home");
    }
    
    [AllowAnonymous]
    [HttpGet("/[action]")]
    public async Task<ActionResult> TelegramLogin([FromQuery] TelegramLoginInfo loginInfo, [FromServices] IOptionsSnapshot<TrackerOptions> configuration)
    {
        if (string.IsNullOrEmpty(loginInfo.Hash))
            return View();
        
        // Check the login info we've been passed
        var query = HttpContext.Request.Query.Select(q =>
            new KeyValuePair<string, string>(q.Key, q.Value.First() ?? throw new InvalidOperationException()));
        var loginWidget = new LoginWidget(configuration.Value.Telegram.AccessToken);
        
        var loginResult = loginWidget.CheckAuthorization(query);
        if (loginResult != Authorization.Valid)
        {
            logger.LogWarning("Failed to login: {FailureReason}", loginResult);
            ViewData["ErrorMessage"] = $"An error occurred while logging in: {loginResult.ToString()}";
            return View();
        }
        
        var user = await UserManager.FindByNameAsync($"tg@{loginInfo.Id}");
        if (user == null) return BadRequest("User not found");
        await UserManager.UpdateSecurityStampAsync(user);
        await signInManager.SignInAsync(user, false, "passwordless-token");
        
        return RedirectToAction("List", "Reminder");
    }

    public class TelegramLoginInfo
    {
        [BindProperty(Name = "id")]
        public long? Id { get; set; }
        
        [BindProperty(Name = "first_name")]
        public string? FirstName { get; set; }
        
        [BindProperty(Name = "last_name")]
        public string? LastName { get; set; }
        
        [BindProperty(Name = "username")]
        public string? Username { get; set; }
        
        [BindProperty(Name = "photo_url")]
        public string? PhotoUrl { get; set; }
        
        [BindProperty(Name = "auth_date")]
        public DateTime? AuthDate { get; set; }
        
        [BindProperty(Name = "hash")]
        public string? Hash { get; set; }
    }
    
    [HttpPut]
    public async Task<ActionResult<UserViewModel>> UpdateCurrentUser([FromBody] UserViewModel model)
    {
        TimeZoneInfo? timeZoneInfo = null;
        if (model.TimeZone != null && !TZConvert.TryGetTimeZoneInfo(model.TimeZone, out timeZoneInfo))
            ModelState.AddModelError("TimeZone", "Time zone not recognized.");
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        var user = await UserManager.GetUserAsync(User);

        if (user is null) return NotFound();

        if (model.TimeZone != null && timeZoneInfo != null)
        {
            user.TimeZoneId = timeZoneInfo.Id;
        }

        await UserManager.UpdateAsync(user);
        return Ok(new UserViewModel(user));
    }
    
    [HttpPost("connect/{telegramUserId:long}")]
    public async Task<ActionResult> ConnectTelegram(long telegramUserId)
    {
        var user = await UserManager.GetUserAsync(User);

        if (user is null) return NotFound();
        
        if (user.TelegramUserId != null) return BadRequest();

        user.TelegramUserId = telegramUserId;
        return Ok();
    }

    public class UserViewModel
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? EmailAddress { get; set; }
        
        [MaxLength(200)]
        public string? TimeZone { get; set; }

        public UserViewModel(ApplicationUser user)
        {
            Id = user.Id;
            UserName = user.UserName;
            EmailAddress = user.Email;
            TimeZone = user.TimeZoneId;
        }
    }
}