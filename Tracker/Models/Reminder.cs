using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class Reminder : BaseModel
{
    [Required] public string UserId { get; init; } = null!;

    [MaxLength(100)]
    [Display(Name = "Name")]
    [Required]
    public string Name { get; set; } = null!;
    
    [MaxLength(100)]
    public string? CronLocal { get; set; }

    public int EveryNTriggers { get; set; } = 1;
    
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    [Required]
    public int ReminderTypeId { get; set; }
    
    public ReminderType? ReminderType { get; set; }

    public int ReminderMinutes { get; set; } = 10;

    public bool IsActionable { get; set; } = true;

    /// <summary>
    /// A random value to prevent multiple completions for one reminder
    /// </summary>
    public int? Nonce { get; set; }
    
    public ICollection<CompletionLink>? CompletionLinks { get; set; }

    public virtual bool IsOwnedBy(string userId) => UserId == userId;
}