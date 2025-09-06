using PadelPassCheckInSystem.Integration.Rekaz.Enums;
using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Models.ViewModels;

public class EndUserSubscriptionsPaginatedViewModel
{
    public PaginatedResult<EndUserSubscription> Subscriptions { get; set; }
    public int? EndUserId { get; set; }
    public string EndUserName { get; set; }
    public string SearchUserName { get; set; }
    public SubscriptionStatus? Status { get; set; }
    
    // Statistics
    public int ActiveCount { get; set; }
    public int PausedCount { get; set; }
    public int PendingCount { get; set; }
    public int CancelledCount { get; set; }
    public int ExpiredCount { get; set; }
    public int StartingSoonCount { get; set; }
}