using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class ReminderMessage : BaseModel
{
    [Required]
    public int ReminderId { get; set; }
    
    public Reminder Reminder { get; set; }
    
    /// <summary>
    /// Telegram message ID
    /// </summary>
    /// <returns></returns>
    public int MessageId { get; set; }
}