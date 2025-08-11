using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class BranchViewModel
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [StringLength(200)]
        public string Address { get; set; }
        
        [Display(Name = "Playtomic Tenant ID")]
        public Guid? PlaytomicTenantId { get; set; }
        
        public bool IsActive { get; set; }
    }
}

