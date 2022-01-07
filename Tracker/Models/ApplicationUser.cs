using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using TimeZoneConverter;

namespace Tracker.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(200)]
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public TimeZoneInfo TimeZone => TZConvert.GetTimeZoneInfo(TimeZoneId);
    
    public long? TelegramUserId { get; set; }
}