using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class EndUserViewModel
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }
        
        [EmailAddress]
        public string Email { get; set; }
        
        [Display(Name = "Image URL")]
        public string ImageUrl { get; set; }
        
        [Required]
        [Display(Name = "Subscription Start Date")]
        public DateTime SubscriptionStartDate { get; set; }
        
        [Required]
        [Display(Name = "Subscription End Date")]
        public DateTime SubscriptionEndDate { get; set; }
        
        public string UniqueIdentifier { get; set; }
    }
}

