using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PadelPassCheckInSystem.Models.Entities;

public class BranchCourt
{
    public int Id { get; set; }
    
    [Required] 
    [StringLength(50)] public string CourtName { get; set; }

    [Required]
    [ForeignKey(nameof(Branch))]
    public int BranchId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Branch Branch { get; set; }
    public virtual ICollection<CheckIn> CheckIns { get; set; }
}