namespace PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

public class SubscriptionPauseHistoryViewModel
{
    public int Id { get; set; }
    public string EndUserName { get; set; }
    public DateTime PauseStartDate { get; set; }
    public DateTime PauseEndDate { get; set; }
    public int PauseDays { get; set; }
    public string Reason { get; set; }
    public string CreatedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}