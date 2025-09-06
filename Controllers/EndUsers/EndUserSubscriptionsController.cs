using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Integration.Rekaz.Enums;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Controllers.EndUsers;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class EndUserSubscriptionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EndUserSubscriptionsController> _logger;

    public EndUserSubscriptionsController(
        ApplicationDbContext context,
        ILogger<EndUserSubscriptionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> EndUserSubscriptions(
        int? endUserId,
        string searchUserName,
        SubscriptionStatus? status,
        int page = 1,
        int pageSize = 10)
    {
        try
        {
            var viewModel = await GetEndUserSubscriptionsAsync(
                endUserId, searchUserName, status, page, pageSize);
            
            return View("~/Views/Admin/EndUserSubscriptions/Index.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading end user subscriptions");
            TempData["Error"] = "An error occurred while loading subscriptions.";
            return RedirectToAction("EndUsers", "EndUser");
        }
    }

    private async Task<EndUserSubscriptionsPaginatedViewModel> GetEndUserSubscriptionsAsync(
        int? endUserId,
        string searchUserName,
        SubscriptionStatus? status,
        int page = 1,
        int pageSize = 10)
    {
        // Build base query
        var query = _context.Set<EndUserSubscription>()
            .Include(s => s.EndUser)
            .AsQueryable();

        // Filter by specific end user if provided
        if (endUserId.HasValue)
        {
            query = query.Where(s => s.EndUserId == endUserId.Value);
        }

        // Filter by user name search
        if (!string.IsNullOrWhiteSpace(searchUserName))
        {
            searchUserName = searchUserName.Trim();
            query = query.Where(s => s.EndUser.Name.Contains(searchUserName) || 
                                   s.EndUser.PhoneNumber.Contains(searchUserName));
        }
        
        // Get statistics for all statuses (before pagination)
        var activeCount = await query.CountAsync(s => s.Status == SubscriptionStatus.Active);
        var pausedCount = await query.CountAsync(s => s.Status == SubscriptionStatus.Paused);
        var pendingCount = await query.CountAsync(s => s.Status == SubscriptionStatus.Pending);
        var cancelledCount = await query.CountAsync(s => s.Status == SubscriptionStatus.Cancelled);
        var expiredCount = await query.CountAsync(s => s.Status == SubscriptionStatus.Expired);
        var startingSoonCount = await query.CountAsync(s => s.Status == SubscriptionStatus.StartingSoon);
        var totalItems = await query.CountAsync();

        // Filter by subscription status
        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        

        // Order by creation date (newest first)
        var orderedQuery = query.OrderByDescending(s => s.StartDate)
                               .ThenByDescending(s => s.Id);

        // Get total count
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        // Apply pagination
        var subscriptions = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get end user name if filtering by specific user
        string endUserName = null;
        if (endUserId.HasValue)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId.Value);
            endUserName = endUser?.Name;
        }

        return new EndUserSubscriptionsPaginatedViewModel
        {
            Subscriptions = new PaginatedResult<EndUserSubscription>
            {
                Items = subscriptions,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize
            },
            EndUserId = endUserId,
            EndUserName = endUserName,
            SearchUserName = searchUserName,
            Status = status,
            ActiveCount = activeCount,
            PausedCount = pausedCount,
            PendingCount = pendingCount,
            CancelledCount = cancelledCount,
            ExpiredCount = expiredCount,
            StartingSoonCount = startingSoonCount
        };
    }
}