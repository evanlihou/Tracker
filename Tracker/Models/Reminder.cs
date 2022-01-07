using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class Reminder : BaseModel
{
    [Required]
    public string UserId { get; set; }
    
    [MaxLength(100)]
    [Display(Name = "Name")]
    [Required]
    public string? Name { get; set; }
    
    [MaxLength(100)]
    public string? CronLocal { get; set; }
    
    public DateTime NextRun { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    [Required]
    public int ReminderTypeId { get; set; }
    
    public ReminderType ReminderType { get; set; }

    public int ReminderMinutes { get; set; } = 10;

    public virtual bool IsOwnedBy(string userId) => UserId == userId;
}