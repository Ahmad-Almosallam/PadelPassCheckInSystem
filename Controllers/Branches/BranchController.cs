using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Controllers.Branches;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class BranchController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Branches()
    {
        var branches = await context.Branches
            .Include(b => b.BranchUsers)
            .OrderBy(b => b.Name)
            .ToListAsync();
        return View("~/Views/Admin/Branches.cshtml", branches);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch(
        BranchViewModel model)
    {
        if (ModelState.IsValid)
        {
            var branch = new Branch
            {
                Name = model.Name,
                Address = model.Address,
                IsActive = true,
                PlaytomicTenantId = model.PlaytomicTenantId,
                TimeZoneId = model.TimeZoneId,
            };

            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            TempData["Success"] = "Branch created successfully!";
        }

        return RedirectToAction(nameof(Branches));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBranch(
        int id,
        BranchViewModel model)
    {
        var branch = await context.Branches.FindAsync(id);
        if (branch != null)
        {
            branch.Name = model.Name;
            branch.Address = model.Address;
            branch.IsActive = model.IsActive;
            branch.PlaytomicTenantId = model.PlaytomicTenantId;
            branch.TimeZoneId = model.TimeZoneId;

            await context.SaveChangesAsync();
            TempData["Success"] = "Branch updated successfully!";
        }

        return RedirectToAction(nameof(Branches));
    }
}