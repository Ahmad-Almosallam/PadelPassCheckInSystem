using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class StopSubscriptionViewModel
    {
        public int EndUserId { get; set; }
        public string EndUserName { get; set; }
        public string EndUserPhoneNumber { get; set; }
        
        [Required(ErrorMessage = "Please provide a reason for stopping the subscription")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string StopReason { get; set; }
    }
}
