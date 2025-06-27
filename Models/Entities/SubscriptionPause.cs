using System.ComponentModel.DataAnnotations;
using PadelPassCheckInSystem.Models.Entities;

public class SubscriptionPause
{
    public int Id { get; set; }
        
    [Required]
    public int EndUserId { get; set; }
        
    [Required]
    public DateTime PauseStartDate { get; set; }
        
    [Required]
    public int PauseDays { get; set; }
        
    public DateTime PauseEndDate { get; set; }
        
    [StringLength(500)]
    public string Reason { get; set; }
        
    [Required]
    public string CreatedByUserId { get; set; }
        
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    public bool IsActive { get; set; } = true;
        
    // Navigation properties
    public virtual EndUser EndUser { get; set; }
    public virtual ApplicationUser CreatedByUser { get; set; }
}