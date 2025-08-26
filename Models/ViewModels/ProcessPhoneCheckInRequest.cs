namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class ProcessPhoneCheckInRequest
    {
        public string PhoneNumber { get; set; }
        
        public DateTime CheckInDate { get; set; } = DateTime.Today;
    }
}

