namespace PadelPassCheckInSystem.Models.ViewModels;

public class EditCheckInRequest
{
    public int CheckInId { get; set; }
    public string CourtName { get; set; }
    public int PlayDurationMinutes { get; set; }
    public DateTime? PlayStartTime { get; set; }
    public string Notes { get; set; }
    public bool PlayerAttended { get; set; } = true;
}