using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Models.ViewModels.EndUserSubscriptionHistories;

public class EndUserSubscriptionHistoryViewModel
{
    public int SubscriptionId { get; set; }
    public string SubscriptionName { get; set; }
    public string SubscriptionCode { get; set; }
    public Guid RekazId { get; set; }
    public string EndUserName { get; set; }
    public string EndUserPhoneNumber { get; set; }
    public PaginatedResult<EndUserSubscriptionHistory> History { get; set; }
}