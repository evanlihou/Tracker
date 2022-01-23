namespace Tracker.Models.ViewModels;

public class ReminderViewModel : Reminder
{
    public ReminderViewModel(TimeZoneInfo timeZone)
    {
        _timeZone = timeZone;
    }
    
    private TimeZoneInfo _timeZone;
    
    public DateTime NextRunLocal => TimeZoneInfo.ConvertTimeFromUtc(NextRun, _timeZone);
    
}