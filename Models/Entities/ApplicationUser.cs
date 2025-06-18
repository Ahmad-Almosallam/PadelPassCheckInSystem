using Microsoft.AspNetCore.Identity;

namespace PadelPassCheckInSystem.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
    }
}
