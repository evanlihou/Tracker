namespace Tracker.Models;

public class OneTimeReminder : BaseReminder
{
    public override string ToString()
    {
        return $"Reminder: {Name}";
    }
}