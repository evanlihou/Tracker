using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeZoneConverter;
using Tracker.Models;
using Tracker.Services;

namespace Tracker.Controllers;

[Authorize]
[Route("user")]
public class UserController : BaseController
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public UserController(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

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
        await _signInManager.SignInAsync(user, false, "passwordless-token");
        
        return RedirectToAction("Index", "Home");
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