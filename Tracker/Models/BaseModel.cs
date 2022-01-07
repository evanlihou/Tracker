using System.ComponentModel.DataAnnotations;

namespace Tracker.Models;

public class BaseModel
{
    [Key]
    public int Id { get; set; }
}