using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.Branches;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class BranchTimeSlotsController(
    ApplicationDbContext context,
    IBranchTimeSlotService timeSlotService) : Controller
{
    // Branch Time Slots Management

    public async Task<IActionResult> BranchTimeSlots(
        int? branchId)
    {
        List<BranchTimeSlotViewModel> timeSlots;

        if (branchId.HasValue)
        {
            timeSlots = await timeSlotService.GetBranchTimeSlotsAsync(branchId.Value);
            var branch = await context.Branches.FindAsync(branchId.Value);
            ViewBag.BranchName = branch?.Name;
        }
        else
        {
            timeSlots = await timeSlotService.GetAllTimeSlotsAsync();
        }

        ViewBag.BranchId = branchId;
        ViewBag.Branches = await context.Branches.Where(b => b.IsActive)
            .ToListAsync();
        ViewBag.DaysOfWeek = Enum.GetValues(typeof(DayOfWeek))
            .Cast<DayOfWeek>()
            .ToList();

        return View("~/Views/Admin/BranchTimeSlots.cshtml", timeSlots);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTimeSlot(
        BranchTimeSlotViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid time slot data.";
            return RedirectToAction("BranchTimeSlots", new { branchId = model.BranchId });
        }

        if (!TimeSpan.TryParse(model.StartTime, out var startTime) ||
            !TimeSpan.TryParse(model.EndTime, out var endTime))
        {
            TempData["Error"] = "Invalid time format. Please use HH:mm format.";
            return RedirectToAction("BranchTimeSlots", new { branchId = model.BranchId });
        }

        var result = await timeSlotService.AddTimeSlotAsync(model.BranchId, model.DayOfWeek, startTime, endTime);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("BranchTimeSlots", new { branchId = model.BranchId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateTimeSlot(
        int id,
        string startTime,
        string endTime,
        bool isActive)
    {
        if (!TimeSpan.TryParse(startTime, out var parsedStartTime) ||
            !TimeSpan.TryParse(endTime, out var parsedEndTime))
        {
            TempData["Error"] = "Invalid time format. Please use HH:mm format.";
            return RedirectToAction("BranchTimeSlots");
        }


        var result = await timeSlotService.UpdateTimeSlotAsync(id, parsedStartTime, parsedEndTime, isActive);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("BranchTimeSlots");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTimeSlot(
        int id)
    {
        var result = await timeSlotService.DeleteTimeSlotAsync(id);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("BranchTimeSlots");
    }
}