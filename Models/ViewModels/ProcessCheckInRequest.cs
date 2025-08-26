namespace PadelPassCheckInSystem.Models.ViewModels;

public class ProcessCheckInRequest
{
    public string Identifier { get; set; }
    
    public DateTime CheckInDate { get; set; } = DateTime.Today;
}