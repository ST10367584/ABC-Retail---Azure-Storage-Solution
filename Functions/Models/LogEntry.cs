namespace ABCRetail.Functions.Models
{
    public class LogEntry
    {
        public string? LogId { get; set; } = Guid.NewGuid().ToString();
        public string? Application { get; set; }
        public string? Level { get; set; } = "Information";
        public string? Message { get; set; }
        public string? Source { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? UserId { get; set; }

        public string ToLogString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Source}] {Message}";
        }
    }
}