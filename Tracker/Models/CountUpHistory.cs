namespace Tracker.Models;

public class CountUpHistory : BaseModel
{
    public int CountUpId { get; set; }
    
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    
    public CountUp CountUp { get; set; }
}