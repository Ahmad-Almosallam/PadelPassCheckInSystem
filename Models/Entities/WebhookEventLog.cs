using PadelPassCheckInSystem.Shared.Enums;

namespace PadelPassCheckInSystem.Models.Entities;

public class WebhookEventLog
{
    public int Id { get; set; }
    public string WebhookEventId { get; set; }
    public string EventName { get; set; }
    public Guid CustomerId { get; set; }
    public string RawData { get; set; }
    public WebhookEventStatus Status { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}