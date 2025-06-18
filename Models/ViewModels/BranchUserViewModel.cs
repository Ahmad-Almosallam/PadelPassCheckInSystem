using System.ComponentModel.DataAnnotations;

namespace CheckInSystem.Web.Models.ViewModels
{
    public class BranchUserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public int? BranchId { get; set; }
        public string BranchName { get; set; }
    }

    public class CreateBranchUserViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }
    }
}