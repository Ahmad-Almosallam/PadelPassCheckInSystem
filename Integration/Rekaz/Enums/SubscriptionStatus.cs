using System.Text.Json.Serialization;

namespace PadelPassCheckInSystem.Integration.Rekaz.Enums;

public enum SubscriptionStatus
{
    [JsonPropertyName("Pending")]
    Pending,
    [JsonPropertyName("Active")]
    Active,
    [JsonPropertyName("Cancelled")]
    Cancelled,
    [JsonPropertyName("Suspended")]
    Suspended,
    [JsonPropertyName("Expired")]
    Expired,
    [JsonPropertyName("Paused")]
    Paused,
    [JsonPropertyName("Transferred")]
    Transferred,
    [JsonPropertyName("StartingSoon")]
    StartingSoon
}