using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public abstract class BaseReminder : BaseModel
{
    [Required] public string UserId { get; init; } = null!;

    [MaxLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = null!;
    
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }

    public int ReminderMinutes { get; set; } = 10;

    /// <summary>
    /// A random value to prevent multiple completions for one reminder
    /// </summary>
    public int? Nonce { get; set; }

    public virtual bool IsOwnedBy(string userId) => UserId == userId;
    public abstract override string ToString();
}