using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.Entities
{
    public class Branch
    {
        public int Id { get; set; }

        [Required] [StringLength(100)] public string Name { get; set; }

        [StringLength(200)] public string Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? PlaytomicTenantId { get; set; }

        // Navigation properties
        public virtual ICollection<ApplicationUser> BranchUsers { get; set; }
        public virtual ICollection<CheckIn> CheckIns { get; set; }
        public virtual ICollection<BranchTimeSlot> TimeSlots { get; set; }
        public virtual ICollection<BranchCourt> BranchCourts { get; set; }
    }
}