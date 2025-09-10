using System.Text.Json.Serialization;
using PadelPassCheckInSystem.Integration.Rekaz.Enums;

namespace PadelPassCheckInSystem.Integration.Rekaz.Models;

public class WebhookEvent
{
    public string Id { get; set; }
    public string EventName { get; set; }
    public DateTime CreatedAt { get; set; }
    public WebhookSubscriptionData Data { get; set; }
}

public class WebhookSubscriptionData
{
    public Guid Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubscriptionStatus Status { get; set; }

    public string CustomStatus { get; set; }
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
    public WebhookCustomer Customer { get; set; }
    public WebhookCustomer FromCustomer { get; set; }
    public WebhookCustomer ToCustomer { get; set; }
    public string Name { get; set; }
    public string Number { get; set; }
    public string Code { get; set; }
    public List<WebhookCustomField> CustomFields { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ResumeAt { get; set; }
    public List<WebhookSubscriptionItem> Items { get; set; }
}

public class WebhookCustomer
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string MobileNumber { get; set; }
    public string Email { get; set; }
}

public class WebhookCustomField
{
    public string Name { get; set; }
    public string Label { get; set; }
    public string Type { get; set; }
    public object Value { get; set; }
}

public class WebhookSubscriptionItem
{
    public string PriceName { get; set; }
    public string PriceId { get; set; }
    public string OptionName { get; set; }
    public string OptionId { get; set; }
    public decimal Price { get; set; }
}