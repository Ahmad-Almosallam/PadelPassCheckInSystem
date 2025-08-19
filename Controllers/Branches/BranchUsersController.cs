using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Controllers.Branches;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class BranchUsersController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> BranchUsers()
    {
        var users = await userManager.Users
            .Include(u => u.Branch)
            .ToListAsync();

        var branchUsers = new List<BranchUserViewModel>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains("BranchUser"))
            {
                branchUsers.Add(new BranchUserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Email = user.Email,
                    BranchId = user.BranchId,
                    BranchName = user.Branch?.Name,
                    IsActive = user.LockoutEnd == null
                });
            }
        }

        ViewBag.Branches = await context.Branches.Where(b => b.IsActive)
            .ToListAsync();

        return View("~/Views/Admin/BranchUsers.cshtml", branchUsers);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranchUser(
        CreateBranchUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                BranchId = model.BranchId,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "BranchUser");
                TempData["Success"] = "Branch user created successfully!";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }
        }

        return RedirectToAction(nameof(BranchUsers));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBranchUser(
        string id,
        string fullName,
        string email,
        int? branchId)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(BranchUsers));
        }

        user.FullName = fullName;
        user.Email = email;
        user.UserName = email;
        user.BranchId = branchId;

        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Branch user updated successfully!";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(BranchUsers));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBranchUser(
        string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(BranchUsers));
        }

        var result = await userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Branch user deleted successfully!";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(BranchUsers));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserStatus(
        string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(BranchUsers));
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = user.LockoutEnd == null
            ? DateTimeOffset.MaxValue
            : // Deactivate
            null; // Activate

        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] =
                $"User has been {(user.LockoutEnd == null ? "activated" : "deactivated")} successfully!";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(BranchUsers));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction("BranchUsers");

        var user = await userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("BranchUsers");
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            TempData["Success"] = "Password has been reset successfully.";
        }
        else
        {
            TempData["Error"] = "Failed to reset password. " +
                                string.Join(" ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction("BranchUsers");
    }
}