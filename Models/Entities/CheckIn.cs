namespace PadelPassCheckInSystem.Models.Entities
{
    public class CheckIn
    {
        public int Id { get; set; }
        public int EndUserId { get; set; }
        public int BranchId { get; set; }
        public DateTime CheckInDateTime { get; set; } = DateTime.UtcNow;
        
        public virtual EndUser EndUser { get; set; }
        public virtual Branch Branch { get; set; }
    }
}

