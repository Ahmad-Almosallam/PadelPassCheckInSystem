using PadelPassCheckInSystem.Integration.Rekaz.Enums;

namespace PadelPassCheckInSystem.Extensions;

public static class SubscriptionStatusExtensions
{
    /// <summary>
    /// Get localized display name for subscription status
    /// </summary>
    public static string GetDisplayName(this SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "Active",
            SubscriptionStatus.Paused => "Paused", 
            SubscriptionStatus.Pending => "Pending",
            SubscriptionStatus.StartingSoon => "Starting Soon",
            SubscriptionStatus.Expired => "Expired",
            SubscriptionStatus.Cancelled => "Cancelled",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Get localized display name in Arabic for subscription status
    /// </summary>
    public static string GetDisplayNameArabic(this SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "نشط",
            SubscriptionStatus.Paused => "متوقف مؤقتاً", 
            SubscriptionStatus.Pending => "قيد الانتظار",
            SubscriptionStatus.StartingSoon => "سيبدأ قريباً",
            SubscriptionStatus.Expired => "منتهي الصلاحية",
            SubscriptionStatus.Cancelled => "ملغي",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Get Bootstrap badge CSS class for subscription status
    /// </summary>
    public static string GetBadgeClass(this SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "bg-success",
            SubscriptionStatus.Paused => "bg-warning text-dark",
            SubscriptionStatus.Pending => "bg-info",
            SubscriptionStatus.StartingSoon => "bg-primary",
            SubscriptionStatus.Expired => "bg-secondary",
            SubscriptionStatus.Cancelled => "bg-danger",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Get Bootstrap icon class for subscription status
    /// </summary>
    public static string GetIconClass(this SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "bi-check-circle",
            SubscriptionStatus.Paused => "bi-pause-circle",
            SubscriptionStatus.Pending => "bi-clock",
            SubscriptionStatus.StartingSoon => "bi-play-circle",
            SubscriptionStatus.Expired => "bi-x-circle",
            SubscriptionStatus.Cancelled => "bi-stop-circle",
            _ => "bi-question-circle"
        };
    }
}