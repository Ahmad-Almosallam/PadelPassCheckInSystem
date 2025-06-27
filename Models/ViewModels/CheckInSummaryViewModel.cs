namespace PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

public class CheckInSummaryViewModel
{
    public int Id { get; set; }
    public string EndUserName { get; set; }
    public string EndUserPhone { get; set; }
    public string EndUserImage { get; set; }
    public string BranchName { get; set; }
    public DateTime CheckInDateTime { get; set; }
    public string CourtName { get; set; }
    public TimeSpan? PlayDuration { get; set; }
    public DateTime? PlayStartTime { get; set; }
    public string Notes { get; set; }
    public bool IsCourtAssigned { get; set; }
}