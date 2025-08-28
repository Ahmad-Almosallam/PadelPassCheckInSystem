using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Models.ViewModels.BranchCourts;

// Models/ViewModels/BranchCourtViewModel.cs
using System.ComponentModel.DataAnnotations;

public class BranchCourtViewModel
{
    public int Id { get; set; }
    public string CourtName { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBranchCourtViewModel
{
    [Required]
    [StringLength(50, ErrorMessage = "Court name cannot exceed 50 characters")]
    [Display(Name = "Court Name")]
    public string CourtName { get; set; }

    [Required] [Display(Name = "Branch")] public int BranchId { get; set; }
}

public class UpdateBranchCourtViewModel
{
    [Required]
    [StringLength(50, ErrorMessage = "Court name cannot exceed 50 characters")]
    [Display(Name = "Court Name")]
    public string CourtName { get; set; }

    [Display(Name = "Active")] public bool IsActive { get; set; }
}

public class BranchCourtsPaginatedViewModel
{
    public PaginatedResult<BranchCourtViewModel> BranchCourts { get; set; } =
        new PaginatedResult<BranchCourtViewModel>();

    public int? FilterBranchId { get; set; }
    public string SearchCourtName { get; set; }
    public List<Branch> Branches { get; set; } = new List<Branch>();
    public string BranchName { get; set; } // For when filtering by specific branch
}