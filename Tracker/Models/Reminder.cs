using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class Reminder : BaseReminder
{
    [MaxLength(100)]
    public string? CronLocal { get; set; }

    public int EveryNTriggers { get; set; } = 1;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    [Required]
    public int ReminderTypeId { get; set; }
    
    public ReminderType? ReminderType { get; set; }

    public bool IsActionable { get; set; } = true;
    public bool IsPendingCompletion { get; set; }

    public List<CompletionLink> CompletionLinks { get; set; } = new List<CompletionLink>();

    public override string ToString()
    {
        return $"Reminder: {ReminderType?.Name ?? "Uncategorized"} - {Name}";
    }
}