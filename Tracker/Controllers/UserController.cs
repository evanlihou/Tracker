using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeZoneConverter;
using Tracker.Models;

namespace Tracker.Controllers;

[ApiController]
[Authorize]
[Route("user")]
public class UserController : BaseController
{
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