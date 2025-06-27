using System.ComponentModel.DataAnnotations;
using PadelPassCheckInSystem.Models.Entities;

public class BranchTimeSlot
{
    public int Id { get; set; }
        
    [Required]
    public int BranchId { get; set; }
        
    [Required]
    public TimeSpan StartTime { get; set; }
        
    [Required]
    public TimeSpan EndTime { get; set; }
        
    [Required]
    public DayOfWeek DayOfWeek { get; set; }
        
    public bool IsActive { get; set; } = true;
        
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    // Navigation properties
    public virtual Branch Branch { get; set; }
}