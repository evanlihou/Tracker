using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class ReminderType : BaseModel
{
    [Required]
    public string UserId { get; set; }
    
    [MaxLength(100)]
    [Required]
    public string? Name { get; set; }
    
    public virtual bool IsOwnedBy(string userId) => UserId == userId;
}