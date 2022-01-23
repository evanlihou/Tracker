using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class ReminderCompletion : BaseModel
{
    [Required]
    public int ReminderId { get; set; }
    
    public Reminder Reminder { get; set; }

    /// <summary>
    /// UTC
    /// </summary>
    /// <returns></returns>
    public DateTime CompletionTime { get; set; }
}