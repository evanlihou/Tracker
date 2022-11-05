using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class CompletionLink : BaseModel
{
    [Required] public string UserId { get; init; } = null!;
    
    [MaxLength(100)]
    [Display(Name = "Name")]
    [Required]
    public string Name { get; set; } = null!;
    
    [Required] public Guid Guid { get; set; }
    
    public ICollection<Reminder>? Reminders { get; set; }
}