using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/{action=Index}")]
public class DashboardController(
    IDashboardAnalyticsService dashboardAnalyticsService,
    ApplicationDbContext context)
    : Controller
{
    #region Dashboard

    public async Task<IActionResult> Index()
    {
        var analytics = await dashboardAnalyticsService.GetDashboardAnalyticsAsync();
        return View("~/Views/Admin/Index.cshtml", analytics);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserLoyaltySegments()
    {
        var data = await dashboardAnalyticsService.GetUserLoyaltySegmentsAsync(context.EndUsers.AsQueryable());
        return Json(new { success = true, data });
    }

    [HttpGet]
    public async Task<IActionResult> GetDropoffAnalysis()
    {
        var data = await dashboardAnalyticsService.GetDropoffAnalysisAsync(context.EndUsers.AsQueryable());
        return Json(new { success = true, data });
    }

    [HttpGet]
    public async Task<IActionResult> GetSubscriptionUtilization()
    {
        var data = await dashboardAnalyticsService.GetSubscriptionUtilizationAsync();
        return Json(new { success = true, data });
    }

    [HttpGet]
    public async Task<IActionResult> GetBranchPerformance()
    {
        var data = await dashboardAnalyticsService.GetBranchPerformanceAsync();
        return Json(new { success = true, data });
    }

    [HttpGet]
    public async Task<IActionResult> GetMultiBranchUsage()
    {
        var data = await dashboardAnalyticsService.GetMultiBranchUsageAsync();
        return Json(new { success = true, data });
    }

    [HttpGet]
    public async Task<IActionResult> GetCheckInTrends()
    {
        var data = await dashboardAnalyticsService.GetCheckInTrendsAsync();
        return Json(new { success = true, data });
    }

    #endregion
}