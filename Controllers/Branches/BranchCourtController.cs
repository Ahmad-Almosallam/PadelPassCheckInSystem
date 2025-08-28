using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Models.ViewModels.BranchCourts;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.Branches;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class BranchCourtController : Controller
{
    private readonly IBranchCourtService _branchCourtService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BranchCourtController> _logger;

    public BranchCourtController(
        IBranchCourtService branchCourtService,
        ApplicationDbContext context,
        ILogger<BranchCourtController> logger)
    {
        _branchCourtService = branchCourtService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> BranchCourts(
        int? branchId,
        string searchCourtName,
        int page = 1,
        int pageSize = 10)
    {
        try
        {
            List<BranchCourtViewModel> allCourts;
            string branchName = null;

            // Get courts based on filter
            if (branchId.HasValue)
            {
                allCourts = await _branchCourtService.GetBranchCourtsAsync(branchId.Value);
                var branch = await _context.Branches.FindAsync(branchId.Value);
                branchName = branch?.Name;
            }
            else
            {
                allCourts = await _branchCourtService.GetAllBranchCourtsAsync();
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchCourtName))
            {
                allCourts = allCourts
                    .Where(c => c.CourtName.Contains(searchCourtName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Apply pagination
            var totalItems = allCourts.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var paginatedCourts = allCourts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new BranchCourtsPaginatedViewModel
            {
                BranchCourts = new PaginatedResult<BranchCourtViewModel>
                {
                    Items = paginatedCourts,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalItems = totalItems,
                    PageSize = pageSize
                },
                FilterBranchId = branchId,
                SearchCourtName = searchCourtName,
                BranchName = branchName,
                Branches = await _context.Branches.AsNoTracking().Where(b => b.IsActive)
                    .OrderBy(b => b.Name)
                    .ToListAsync()
            };

            return View("~/Views/Admin/BranchCourts/Index.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading branch courts");
            TempData["Error"] = "An error occurred while loading courts.";
            return RedirectToAction("Index", "Dashboard");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBranchCourt(
        CreateBranchCourtViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _branchCourtService.CreateBranchCourtAsync(model);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }
        }
        else
        {
            TempData["Error"] = "Please correct the validation errors and try again.";
        }

        // Return to the same page with filters preserved
        var routeValues = new
        {
            branchId = Request.Form["FilterBranchId"]
                .ToString()
        };
        return RedirectToAction(nameof(BranchCourts), routeValues);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBranchCourt(
        int id,
        UpdateBranchCourtViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _branchCourtService.UpdateBranchCourtAsync(id, model);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }
        }
        else
        {
            TempData["Error"] = "Please correct the validation errors and try again.";
        }

        return RedirectToAction(nameof(BranchCourts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBranchCourt(
        int id)
    {
        var result = await _branchCourtService.DeleteBranchCourtAsync(id);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(BranchCourts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCourtStatus(
        int id)
    {
        var result = await _branchCourtService.ToggleCourtStatusAsync(id);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(BranchCourts));
    }

    [HttpGet]
    public async Task<IActionResult> GetCourtsByBranch(
        int branchId)
    {
        try
        {
            var courts = await _branchCourtService.GetActiveCourtsByBranchAsync(branchId);
            var courtOptions = courts.Select(c => new { value = c.Id, text = c.CourtName })
                .ToList();

            return Json(new { success = true, courts = courtOptions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting courts for branch {BranchId}", branchId);
            return Json(new { success = false, message = "Failed to load courts." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CheckCanDelete(
        int id)
    {
        try
        {
            var canDelete = await _branchCourtService.CanDeleteCourtAsync(id);
            return Json(new { canDelete });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if court {Id} can be deleted", id);
            return Json(new { canDelete = false });
        }
    }
}