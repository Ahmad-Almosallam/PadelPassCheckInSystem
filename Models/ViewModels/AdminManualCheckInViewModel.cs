using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels;

public class AdminManualCheckInViewModel
{
    [Required]
    [Phone]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public int BranchId { get; set; }

    [Required]
    [Display(Name = "Check-In Date")]
    public DateTime CheckInDate { get; set; }

    [Required]
    [Display(Name = "Check-In Time")]
    public TimeSpan CheckInTime { get; set; }

    [Display(Name = "Court Name/Number")]
    [StringLength(50)]
    public string CourtName { get; set; }

    [Display(Name = "Play Duration (minutes)")]
    [Range(30, 300, ErrorMessage = "Play duration must be between 30 and 300 minutes")]
    public int? PlayDurationMinutes { get; set; }

    [Display(Name = "Play Start Time")]
    public TimeSpan? PlayStartTime { get; set; }

    [Display(Name = "Notes")]
    [StringLength(200)]
    public string Notes { get; set; }

    // For display purposes
    public string BranchName { get; set; }
    public string EndUserName { get; set; }
}

public class AdminManualCheckInRequest
{
    public string PhoneNumber { get; set; }
    public int BranchId { get; set; }
    public DateTime CheckInDateTime { get; set; }  // Combined date and time in KSA
    public string CourtName { get; set; }
    public int? PlayDurationMinutes { get; set; }
    public DateTime? PlayStartTime { get; set; }  // In KSA time
    public string Notes { get; set; }
    public bool PlayerAttended { get; set; } = true;
}