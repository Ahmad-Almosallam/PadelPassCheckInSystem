using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        
        [ForeignKey(nameof(BranchCourt))]
        public int? BranchCourtId { get; set; }

        [ForeignKey(nameof(EndUserSubscription))]
        public int? EndUserSubscriptionId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public virtual BranchCourt BranchCourt { get; set; }
        public virtual EndUser EndUser { get; set; }
        public virtual Branch Branch { get; set; }
        public virtual EndUserSubscription EndUserSubscription { get; set; }
    }
}

