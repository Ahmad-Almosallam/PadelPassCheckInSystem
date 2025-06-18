namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalBranches { get; set; }
        public int TotalEndUsers { get; set; }
        public int TotalCheckInsToday { get; set; }
        public int ActiveSubscriptions { get; set; }
    }
}

