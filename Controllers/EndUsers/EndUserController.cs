using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
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
    IPlaytomicSyncService playtomicSyncService)
    : Controller
{
    public async Task<IActionResult> EndUsers(
        string searchPhoneNumber,
        int page = 1,
        int pageSize = 10)
    {
        // Build base filtered query (without pagination)
        var baseQuery = context.EndUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchPhoneNumber))
        {
            searchPhoneNumber = searchPhoneNumber.Trim();
            baseQuery = baseQuery.Where(e => e.PhoneNumber.Contains(searchPhoneNumber));
        }

        // Compute statistics via SQL (before pagination)
        var todayKsaDate = KSADateTimeExtensions.GetKSANow()
            .Date;
        var startOfKsaDayUtc = todayKsaDate.GetStartOfKSADayInUTC();
        var endOfKsaDayUtc = todayKsaDate.GetEndOfKSADayInUTC();

        var activeSubscriptions = await baseQuery
            .CountAsync(u => u.SubscriptionStartDate <= endOfKsaDayUtc
                             && u.SubscriptionEndDate >= startOfKsaDayUtc
                             && !(
                                 u.IsPaused &&
                                 u.CurrentPauseStartDate != null &&
                                 u.CurrentPauseEndDate != null &&
                                 u.CurrentPauseStartDate <= endOfKsaDayUtc &&
                                 u.CurrentPauseEndDate >= startOfKsaDayUtc
                             )
                             && !u.IsStopped);

        var currentlyPaused = await baseQuery
            .CountAsync(u => u.IsPaused
                             && !u.IsStopped
                             && u.CurrentPauseStartDate != null
                             && u.CurrentPauseEndDate != null
                             && u.CurrentPauseStartDate <= endOfKsaDayUtc
                             && u.CurrentPauseEndDate >= startOfKsaDayUtc);

        var stoppedCount = await baseQuery.CountAsync(u => u.IsStopped);

        var expiredCount = await baseQuery
            .CountAsync(u => !u.IsStopped && u.SubscriptionEndDate < startOfKsaDayUtc);

        var notSetPlaytomicUserIdsCount = await baseQuery.CountAsync(u => u.PlaytomicUserId == null);

        var stoppedByWarningsCount = await baseQuery.CountAsync(u => u.IsStoppedByWarning);

        // Pagination remains in DB
        var orderedQuery = baseQuery.OrderByDescending(e => e.CreatedAt);

        var totalItems = await orderedQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        var endUsers = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var viewModel = new EndUsersPaginatedViewModel
        {
            EndUsers = new PaginatedResult<EndUser>
            {
                Items = endUsers,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize
            },
            SearchPhoneNumber = searchPhoneNumber,
            ActiveSubscriptions = activeSubscriptions,
            CurrentlyPaused = currentlyPaused,
            StoppedCount = stoppedCount,
            ExpiredCount = expiredCount,
            NotSetPlaytomicUserIdsCount = notSetPlaytomicUserIdsCount,
            StoppedByWarningsCount = stoppedByWarningsCount
        };

        return View("~/Views/Admin/EndUsers.cshtml", viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEndUser(
        EndUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Check if the end user already exists by phone number or email
            var existingUser = await context.EndUsers
                .FirstOrDefaultAsync(e => e.PhoneNumber == model.PhoneNumber || e.Email == model.Email.ToLower());

            if (existingUser != null)
            {
                TempData["Error"] = "An end user with the same phone number or email already exists.";
                return RedirectToAction(nameof(EndUsers));
            }

            // Convert KSA dates to UTC for storage
            var subscriptionStartUtc = model.SubscriptionStartDate;
            var subscriptionEndUtc = model.SubscriptionEndDate;

            var endUser = new EndUser
            {
                Name = model.Name,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email.ToLower(),
                ImageUrl = model.ImageUrl,
                SubscriptionStartDate = subscriptionStartUtc,
                SubscriptionEndDate = subscriptionEndUtc,
                UniqueIdentifier = Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 8)
                    .ToUpper()
            };

            context.EndUsers.Add(endUser);
            await context.SaveChangesAsync();

            TempData["Success"] = "End user created successfully!";
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
        var endUser = await context.EndUsers.FindAsync(id);

        if (endUser == null || !ModelState.IsValid) return RedirectToAction(nameof(EndUsers));

        if ((endUser.PhoneNumber != model.PhoneNumber &&
             await context.EndUsers.AnyAsync(e => e.PhoneNumber == model.PhoneNumber)) ||
            (endUser.Email != model.Email.ToLower() &&
             await context.EndUsers.AnyAsync(e => e.Email == model.Email.ToLower())))
        {
            TempData["Error"] = "An end user with the same phone number or email already exists.";
            return RedirectToAction(nameof(EndUsers));
        }

        // Convert KSA dates to UTC for storage
        var subscriptionStartUtc = model.SubscriptionStartDate;
        var subscriptionEndUtc = model.SubscriptionEndDate;

        endUser.Name = model.Name;
        endUser.PhoneNumber = model.PhoneNumber;
        endUser.Email = model.Email;
        endUser.ImageUrl = model.ImageUrl;
        endUser.SubscriptionStartDate = subscriptionStartUtc;
        endUser.SubscriptionEndDate = subscriptionEndUtc;

        await context.SaveChangesAsync();
        TempData["Success"] = "End user updated successfully!";

        return RedirectToAction(nameof(EndUsers));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEndUser(
        int id)
    {
        var endUser = await context.EndUsers.FindAsync(id);

        if (endUser == null) return RedirectToAction(nameof(EndUsers));

        context.EndUsers.Remove(endUser);
        await context.SaveChangesAsync();
        TempData["Success"] = "End user deleted successfully!";

        return RedirectToAction(nameof(EndUsers));
    }

    // Generate QR Code
    [HttpGet]
    public async Task<IActionResult> GenerateQRCode(
        int endUserId,
        bool forceRegenerate = false)
    {
        var endUser = await context.EndUsers.FindAsync(endUserId);
        if (endUser == null)
        {
            return NotFound();
        }

        // Check if QR has already been downloaded and not forcing regeneration
        if (endUser.HasDownloadedQR && !forceRegenerate)
        {
            return Json(new { success = false, message = "QR code has already been downloaded." });
        }

        // Generate a new token and reset the download status
        endUser.QRCodeDownloadToken = Guid.NewGuid()
            .ToString("N");
        endUser.HasDownloadedQR = false;
        await context.SaveChangesAsync();

        // Generate the download URL
        var downloadUrl = Url.Action("Download", "QRCode", new { token = endUser.QRCodeDownloadToken },
            Request.Scheme);

        return Json(new
        {
            success = true,
            downloadUrl = downloadUrl,
            message = forceRegenerate
                ? "New QR code generated successfully."
                : "QR code download link generated successfully."
        });
    }

    [HttpPost]
    public async Task<IActionResult> StopSubscription(
        StopSubscriptionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var endUser = await context.EndUsers.FindAsync(model.EndUserId);
        if (endUser == null)
        {
            TempData["Error"] = "End user not found.";
            return RedirectToAction("EndUsers");
        }

        if (endUser.IsStopped)
        {
            TempData["Error"] = "Subscription is already stopped.";
            return RedirectToAction("EndUsers");
        }

        // Stop the subscription
        endUser.IsStopped = true;
        endUser.StoppedDate = DateTime.UtcNow;
        endUser.StopReason = model.StopReason;

        // If the subscription was paused, unpause it when stopping
        if (endUser.IsPaused)
        {
            endUser.IsPaused = false;
            endUser.CurrentPauseStartDate = null;
            endUser.CurrentPauseEndDate = null;
        }

        try
        {
            await context.SaveChangesAsync();
            TempData["Success"] = $"Subscription for {endUser.Name} has been stopped successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping subscription for EndUser ID: {EndUserId}", model.EndUserId);
            TempData["Error"] = "An error occurred while stopping the subscription.";
        }

        return RedirectToAction("EndUsers");
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateSubscription(
        int endUserId)
    {
        var endUser = await context.EndUsers.FindAsync(endUserId);
        if (endUser == null)
        {
            TempData["Error"] = "End user not found.";
            return RedirectToAction("EndUsers");
        }

        if (!endUser.IsStopped)
        {
            TempData["Error"] = "Subscription is not stopped.";
            return RedirectToAction("EndUsers");
        }

        // Reactivate the subscription
        endUser.IsStopped = false;
        endUser.StoppedDate = null;
        endUser.StopReason = null;

        if (endUser.IsStoppedByWarning)
        {
            endUser.IsStoppedByWarning = false;
            endUser.WarningCount = 0;
        }

        try
        {
            await context.SaveChangesAsync();
            TempData["Success"] = $"Subscription for {endUser.Name} has been reactivated successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reactivating subscription for EndUser ID: {EndUserId}", endUserId);
            TempData["Error"] = "An error occurred while reactivating the subscription.";
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
}