using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.Entities
{
    public class CheckIn
    {
        public int Id { get; set; }
        public int EndUserId { get; set; }
        public int BranchId { get; set; }
        public DateTime CheckInDateTime { get; set; } = DateTime.UtcNow;
        
        
        [StringLength(50)]
        public string CourtName { get; set; }
        
        public TimeSpan? PlayDuration { get; set; }
        
        public DateTime? PlayStartTime { get; set; }
        
        [StringLength(200)]
        public string Notes { get; set; }

        public bool PlayerAttended { get; set; }
        
        public virtual EndUser EndUser { get; set; }
        public virtual Branch Branch { get; set; }
    }
}

