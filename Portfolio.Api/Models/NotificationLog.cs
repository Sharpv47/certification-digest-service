namespace Portfolio.Api.Models;

public class NotificationLog
{
    public int Id { get; set; }

    // e.g. "Digest:60days"
    public string NotificationType { get; set; } = string.Empty;

    // e.g. "2026-W07"
    public string PeriodKey { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
