namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class CheckInViewModel
    {
        public int Id { get; set; }
        public DateTime CheckInDate { get; set; }
        public string UserName { get; set; }
        public string BranchName { get; set; }
        public string Status { get; set; }
        public string CourtName { get; set; }
        public TimeSpan? PlayDuration { get; set; }
        public DateTime? PlayStartTime { get; set; }
        public string Notes { get; set; }
    }
}

