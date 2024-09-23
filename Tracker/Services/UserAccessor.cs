using Microsoft.AspNetCore.Identity;
using TimeZoneConverter;
using Tracker.Models;

namespace Tracker.Services;

public class UserAccessor(IHttpContextAccessor httpContextAccessor, UserManager<ApplicationUser> userManager)
{
    public async Task<TimeZoneInfo?> GetUserTimeZone()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null) throw new ApplicationException("Could not get user from HTTP Context");
        
        return TZConvert.GetTimeZoneInfo((await userManager.GetUserAsync(user))?.TimeZoneId ?? "Etc/UTC");
    }
}