using System.Text.Json.Serialization;
using PadelPassCheckInSystem.Integration.Rekaz.Enums;

namespace PadelPassCheckInSystem.Integration.Rekaz.Models;

public class SubscriptionsResponse
{
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("items")] public List<SubscriptionResponse> Items { get; set; } = [];
}

public class SubscriptionResponse
{
    [JsonPropertyName("id")] public Guid Id { get; set; }

    [JsonPropertyName("customerId")] public Guid CustomerId { get; set; }
    [JsonPropertyName("startAt")] public DateTime StartAt { get; set; }
    [JsonPropertyName("endAt")] public DateTime EndAt { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubscriptionStatus Status { get; set; }

    [JsonPropertyName("discount")] public DiscountResponse Discount { get; set; }

    [JsonPropertyName("paidAmount")] public decimal PaidAmount { get; set; }
    [JsonPropertyName("totalAmount")] public decimal TotalAmount { get; set; }
    [JsonPropertyName("isPaused")] public bool IsPaused { get; set; }
    [JsonPropertyName("pausedAt")] public DateTime? PausedAt { get; set; }
    [JsonPropertyName("resumeAt")] public DateTime? ResumeAt { get; set; }
    [JsonPropertyName("items")] public List<ItemResponse> Items { get; set; } = [];
    [JsonPropertyName("name")] public string Code { get; set; }
}

public class ItemResponse
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class DiscountResponse
{
    [JsonPropertyName("value")] public decimal Value { get; set; }
}