using System.ComponentModel.DataAnnotations;

namespace Tracker;

public class TrackerOptions
{
    [Required]
    public DbProvider DbProvider { get; set; }
    
    public Dictionary<ConnectionStrings, string> ConnectionStrings { get; set; }
    
    public string BaseUrl { get; set; }
    
    public TrackerTelegramOptions Telegram { get; set; }
}

public class TrackerTelegramOptions
{
    public string BaseUrl { get; set; }
    public string AccessToken { get; set; }
}

public enum DbProvider
{
    SQLite,
    MySQL
}

public enum ConnectionStrings
{
    DefaultConnection
}