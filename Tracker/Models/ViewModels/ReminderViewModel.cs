namespace Tracker.Models.ViewModels;

public class ReminderViewModel : Reminder
{
    public ReminderViewModel(TimeZoneInfo timeZone)
    {
        _timeZone = timeZone;
    }
    
    private readonly TimeZoneInfo _timeZone;
    
    public DateTime? NextRunLocal => (NextRun != null ? TimeZoneInfo.ConvertTimeFromUtc((DateTime) NextRun, _timeZone) : null);
    
}