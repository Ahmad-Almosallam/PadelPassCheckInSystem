using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.Entities
{
    public class EndUser
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        [Phone]
        public string PhoneNumber { get; set; }
        
        [EmailAddress]
        public string Email { get; set; }
        
        public string ImageUrl { get; set; }
        
        [Required]
        public DateTime SubscriptionStartDate { get; set; }
        
        [Required]
        public DateTime SubscriptionEndDate { get; set; }
        
        public string UniqueIdentifier { get; set; } // For QR code
        
        public string QRCodeDownloadToken { get; set; }
        public bool HasDownloadedQR { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public virtual ICollection<CheckIn> CheckIns { get; set; }
    }
}

