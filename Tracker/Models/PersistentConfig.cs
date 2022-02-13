namespace Tracker.Models
{
    public class PersistentConfig : BaseModel
    {
        public string ConfigCode { get; set; } = null!;
        public string Value { get; set; } = null!;
    }
}