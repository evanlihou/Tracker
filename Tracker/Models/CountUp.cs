using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class CountUp : BaseModel
{
    [Required] public required string UserId { get; init; } = null!;

    [Required]
    [MaxLength(128)]
    [Display(Name = "Name")]
    public required string Name { get; set; }
    
    public DateTime? CountingFromUtc { get; set; }
    
    public virtual ICollection<CountUpHistory>? Histories { get; set; }
}