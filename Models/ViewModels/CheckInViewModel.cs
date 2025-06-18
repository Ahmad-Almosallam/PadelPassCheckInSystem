namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class CheckInViewModel
    {
        public int Id { get; set; }
        public DateTime CheckInDate { get; set; }
        public string UserName { get; set; }
        public string BranchName { get; set; }
        public string Status { get; set; }
    }
}

