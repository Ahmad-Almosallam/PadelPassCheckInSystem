using System.ComponentModel.DataAnnotations;

namespace PadelPassCheckInSystem.Models.ViewModels
{
    public class ResetPasswordViewModel
    {
        public string UserId { get; set; }
        
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; }
    }
}
