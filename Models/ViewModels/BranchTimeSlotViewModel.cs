using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

public class BranchTimeSlotViewModel
{
    public int Id { get; set; }
        
    [Required]
    public int BranchId { get; set; }
        
    [Required]
    [Display(Name = "Day of Week")]
    public DayOfWeek DayOfWeek { get; set; }
        
    [Required]
    [Display(Name = "Start Time")]
    public string StartTime { get; set; }
        
    [Required]
    [Display(Name = "End Time")]
    public string EndTime { get; set; }
        
    public bool IsActive { get; set; } = true;
        
    // For display
    public string BranchName { get; set; }
    public string TimeRange { get; set; }
}