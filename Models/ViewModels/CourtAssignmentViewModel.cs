using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

public class CourtAssignmentViewModel
{
    [Required]
    public int CheckInId { get; set; }
        
    [Required]
    [Display(Name = "Court Name/Number")]
    [StringLength(50, ErrorMessage = "Court name cannot exceed 50 characters")]
    public string CourtName { get; set; }
        
    [Display(Name = "Play Duration (minutes)")]
    [Range(30, 300, ErrorMessage = "Play duration must be between 30 and 300 minutes")]
    public int PlayDurationMinutes { get; set; }
        
    [Display(Name = "Play Start Time")]
    public DateTime? PlayStartTime { get; set; }
        
    [Display(Name = "Notes")]
    [StringLength(200, ErrorMessage = "Notes cannot exceed 200 characters")]
    public string Notes { get; set; }
        
    // Read-only properties for display
    public string EndUserName { get; set; }
    public DateTime CheckInDateTime { get; set; }
    public string BranchName { get; set; }
}