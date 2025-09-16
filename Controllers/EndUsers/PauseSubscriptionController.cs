using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.EndUsers;

[Authorize(Roles = "Admin")]
[Route("admin/[action]")]
public class PauseSubscriptionController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PauseSubscriptionController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Subscription Pause Management with KSA time validation
    [HttpGet]
    public async Task<IActionResult> PauseSubscription(
        int endUserId)
    {
        var endUser = await _context.EndUsers.FindAsync(endUserId);
        if (endUser == null)
        {
            TempData["Error"] = "End user not found.";
            return RedirectToAction("EndUsers", "EndUser");
        }

        if (endUser.IsPaused)
        {
            TempData["Error"] = "Subscription is already paused.";
            return RedirectToAction("EndUsers", "EndUser");
        }

        var viewModel = new PauseSubscriptionViewModel
        {
            EndUserId = endUserId,
            EndUserName = endUser.Name,
            CurrentSubscriptionEndDate = endUser.SubscriptionEndDate.ToKSATime(), // Convert to KSA for display
            PauseStartDate = KSADateTimeExtensions.GetKSANow()
                .Date, // Use KSA date
            PauseDays = 7 // Default 7 days
        };

        return View("~/Views/Admin/PauseSubscription.cshtml", viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> PauseSubscription(
        PauseSubscriptionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (DateTime.UtcNow.AddDays(2).Date > model.PauseStartDate.Date)
        {
            TempData["Error"] = "Pause Start date must be two days from now";
            return RedirectToAction("EndUsers", "EndUser");
        }

        var pauseService = HttpContext.RequestServices.GetRequiredService<ISubscriptionPauseService>();
        var currentUserId = _userManager.GetUserId(User);

        // Note: PauseStartDate is already in KSA time from the form
        var result = await pauseService.PauseSubscriptionAsync(
            model.EndUserId,
            model.PauseStartDate, // This is in KSA time
            model.PauseDays,
            model.Reason,
            currentUserId
        );

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("EndUsers", "EndUser");
    }

    [HttpPost]
    public async Task<IActionResult> UnpauseSubscription(
        int endUserId, DateTime? unpauseDate = null)
    {
        var pauseService = HttpContext.RequestServices.GetRequiredService<ISubscriptionPauseService>();
        var currentUserId = _userManager.GetUserId(User);

        // Use the provided unpause date or default to null (current date will be used in service)
        var result = await pauseService.UnpauseSubscriptionAsync(endUserId, currentUserId, unpauseDate);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("EndUsers", "EndUser");
    }

    [HttpGet]
    public async Task<IActionResult> PauseHistory(
        int? endUserId)
    {
        var pauseService = HttpContext.RequestServices.GetRequiredService<ISubscriptionPauseService>();
        List<SubscriptionPauseHistoryViewModel> pauseHistory;

        if (endUserId.HasValue)
        {
            pauseHistory = await pauseService.GetPauseHistoryAsync(endUserId.Value);
            var endUser = await _context.EndUsers.FindAsync(endUserId.Value);
            ViewBag.EndUserName = endUser?.Name;
        }
        else
        {
            pauseHistory = await pauseService.GetAllPauseHistoryAsync();
        }

        ViewBag.EndUserId = endUserId;
        return View("~/Views/Admin/PauseHistory.cshtml", pauseHistory);
    }
}