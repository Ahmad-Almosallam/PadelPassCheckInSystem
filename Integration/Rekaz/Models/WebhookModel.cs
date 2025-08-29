using PadelPassCheckInSystem.Integration.Rekaz.Enums;

namespace PadelPassCheckInSystem.Integration.Rekaz.Models;

public class WebhookModel
{
    public Guid Id { get; set; }
    public CustomWebhookModel Customer { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
}

public class CustomWebhookModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
}