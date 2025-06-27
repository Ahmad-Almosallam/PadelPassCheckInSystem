using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

public class PauseSubscriptionViewModel
{
    [Required]
    public int EndUserId { get; set; }
        
    [Required]
    [Display(Name = "Pause Start Date")]
    public DateTime PauseStartDate { get; set; }
        
    [Required]
    [Range(1, 365, ErrorMessage = "Pause days must be between 1 and 365")]
    [Display(Name = "Number of Days to Pause")]
    public int PauseDays { get; set; }
        
    [Display(Name = "Reason for Pause")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; }
        
    // Read-only properties for display
    public string EndUserName { get; set; }
    public DateTime CurrentSubscriptionEndDate { get; set; }
    public DateTime NewSubscriptionEndDate { get; set; }
}