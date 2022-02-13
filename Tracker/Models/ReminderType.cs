using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class ReminderType : BaseModel
{
    [Required] public string UserId { get; init; } = null!;

    [MaxLength(100)] [Required] public string Name { get; set; } = null!;
    
    public virtual bool IsOwnedBy(string userId) => UserId == userId;
}