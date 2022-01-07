namespace Tracker.Models
{
    public class PersistentConfig : BaseModel
    {
        public string ConfigCode { get; set; }
        public string Value { get; set; }
    }
}