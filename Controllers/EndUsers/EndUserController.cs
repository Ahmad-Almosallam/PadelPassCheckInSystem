using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Integration.Rekaz;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.EndUsers;

[Authorize(Roles = "Admin")]
[Route("Admin/{action=Index}")]
public class EndUserController(
    ApplicationDbContext context,
    ILogger<EndUserController> logger,
    IPlaytomicIntegrationService playtomicIntegrationService,
    IPlaytomicSyncService playtomicSyncService,
    IEndUserService endUserService,
    RekazClient rekazClient)
    : Controller
{
    public async Task<IActionResult> EndUsers(
        string searchPhoneNumber,
        string status,
        int page = 1,
        int pageSize = 10)
    {
        var viewModel = await endUserService.GetEndUsersAsync(searchPhoneNumber, status, page, pageSize);
        return View("~/Views/Admin/EndUsers.cshtml", viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEndUser(
        EndUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await endUserService.CreateEndUserAsync(model);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(EndUsers));
        }

        TempData["Error"] = string.Join(" | ",
            ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

        return RedirectToAction(nameof(EndUsers));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEndUser(
        int id,
        EndUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(EndUsers));
        }

        var result = await endUserService.UpdateEndUserAsync(id, model);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(EndUsers));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEndUser(
        int id)
    {
        var result = await endUserService.DeleteEndUserAsync(id);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(EndUsers));
    }

    // Generate QR Code
    [HttpGet]
    public async Task<IActionResult> GenerateQRCode(
        int endUserId,
        bool forceRegenerate = false)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await endUserService.GenerateQRCodeAsync(endUserId, forceRegenerate, baseUrl);

        if (result.Success)
        {
            return Json(new
            {
                success = true,
                downloadUrl = result.DownloadUrl,
                message = result.Message
            });
        }
        else
        {
            return Json(new { success = false, message = result.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> StopSubscription(
        StopSubscriptionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await endUserService.StopSubscriptionAsync(model.EndUserId, model.StopReason);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("EndUsers");
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateSubscription(
        int endUserId)
    {
        var result = await endUserService.ReactivateSubscriptionAsync(endUserId);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction("EndUsers");
    }

    #region Playtomic Integration

    [HttpGet]
    public async Task<IActionResult> GetSyncPreview()
    {
        try
        {
            // Get active users count
            var activeUsersCount = await playtomicSyncService.GetActiveUsersAsync();

            // Get branches with tenant ID count
            var branchesWithTenantCount = await context.Branches
                .CountAsync(b => b.PlaytomicTenantId.HasValue && b.IsActive);

            return Json(new
            {
                success = true,
                activeUsers = activeUsersCount.Count,
                branches = branchesWithTenantCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting sync preview: {Error}", ex.Message);
            return Json(new { success = false, message = "Error loading preview data." });
        }
    }

    #endregion

    #region Playtomic Integration Management

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPlaytomicIntegration()
    {
        try
        {
            var integration = await playtomicIntegrationService.GetActiveIntegrationAsync();

            if (integration == null)
            {
                return Json(new { success = false, message = "No integration configured" });
            }

            var viewModel = new PlaytomicIntegrationViewModel
            {
                Id = integration.Id,
                AccessToken = integration.AccessToken,
                AccessTokenExpiration = integration.AccessTokenExpiration.ToKSATime(),
                RefreshToken = integration.RefreshToken,
                RefreshTokenExpiration = integration.RefreshTokenExpiration.ToKSATime(),
            };

            return Json(new { success = true, integration = viewModel });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Playtomic integration");
            return Json(new { success = false, message = "Error loading integration data" });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SavePlaytomicIntegration(
        [FromBody] PlaytomicIntegrationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid integration data" });
        }

        try
        {
            var integration = await playtomicIntegrationService.SaveIntegrationAsync(model);

            return Json(new
            {
                success = true,
                message = "Integration saved successfully",
                integration = new PlaytomicIntegrationViewModel
                {
                    Id = integration.Id,
                    AccessToken = integration.AccessToken,
                    AccessTokenExpiration = integration.AccessTokenExpiration,
                    RefreshToken = integration.RefreshToken,
                    RefreshTokenExpiration = integration.RefreshTokenExpiration,
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving Playtomic integration");
            return Json(new { success = false, message = "Error saving integration data" });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncUsersWithIntegration()
    {
        try
        {
            // Get valid access token (will refresh if needed)
            var accessToken = await playtomicIntegrationService.GetValidAccessTokenAsync();

            var result = await playtomicSyncService.SyncActiveUsersToPlaytomicAsync(accessToken);

            if (result.IsSuccess)
            {
                var message =
                    $"Sync completed! Successfully synced {result.TotalUsers} users to {result.SuccessfulBranches}/{result.TotalBranches} branches.";

                if (result.FailedBranches > 0)
                {
                    message += $" {result.FailedBranches} branches failed.";
                }

                return Json(new
                {
                    success = true,
                    message = message,
                    result = new
                    {
                        totalBranches = result.TotalBranches,
                        successfulBranches = result.SuccessfulBranches,
                        failedBranches = result.FailedBranches,
                        totalUsers = result.TotalUsers,
                        branchResults = result.BranchResults.Select(br => new
                        {
                            branchName = br.BranchName,
                            tenantId = br.TenantId,
                            isSuccess = br.IsSuccess,
                            errorMessage = br.ErrorMessage,
                            userCount = br.UserCount
                        })
                    }
                });
            }
            else
            {
                return Json(new
                {
                    success = false,
                    message = result.ErrorMessage ?? "Sync failed with unknown error.",
                    result = new
                    {
                        totalBranches = result.TotalBranches,
                        successfulBranches = result.SuccessfulBranches,
                        failedBranches = result.FailedBranches,
                        totalUsers = result.TotalUsers,
                        branchResults = result.BranchResults.Select(br => new
                        {
                            branchName = br.BranchName,
                            tenantId = br.TenantId,
                            isSuccess = br.IsSuccess,
                            errorMessage = br.ErrorMessage,
                            userCount = br.UserCount
                        })
                    }
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message, requiresSetup = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Playtomic sync with integration");
            return Json(new { success = false, message = $"An error occurred during sync: {ex.Message}" });
        }
    }


    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncPlaytomicUserIds()
    {
        try
        {
            var updatedCount = await playtomicIntegrationService.SyncCategoryMembersPlaytomicUserIdsAsync();

            return Json(new { success = true, updatedCount });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Playtomic integration");
            return Json(new { success = false, message = "Error loading integration data" });
        }
    }

    #endregion

    #region Rekaz

    public async Task<IActionResult> SyncRekaz()
    {
        var customers = await rekazClient.GetCustomersAsync(int.MaxValue);

        // call sync endusers
        var syncResult = await endUserService.SyncRekazAsync(customers.Items);

        if (syncResult.Success)
        {
            return Json(new { success = true, message = syncResult.Message });
        }

        return Json(new { success = false, message = syncResult.Message });
    }

    #endregion
}