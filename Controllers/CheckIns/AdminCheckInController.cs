using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.CheckIns;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class AdminCheckInController : CheckInBaseController
{
    private readonly IExcelService _excelService;
    private readonly ApplicationDbContext _context;
    private readonly ICheckInService _checkInService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWarningService _warningService;

    public AdminCheckInController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICheckInService checkInService,
        IExcelService excelService,
        IWarningService warningService) : base(excelService, context, checkInService, userManager)
    {
        _context = context;
        _userManager = userManager;
        _checkInService = checkInService;
        _excelService = excelService;
        _warningService = warningService;
    }

    public async Task<IActionResult> EditCheckIn(
        [FromBody] EditCheckInRequest request)
    {
        var result = await _checkInService.EditCheckInAsync(request);
        return Json(new { success = result.Success, message = result.Message });
    }


    [HttpPost]
    public async Task<IActionResult> ValidateUserForManualCheckIn(
        [FromBody] ValidateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.PhoneNumber) || request.CheckInDate == default)
        {
            return Json(new { success = false, message = "Invalid phone number or check-in date." });
        }

        var (isValid, message, endUser) = await _checkInService.ValidateEndUserForManualCheckInAsync(
            request.PhoneNumber.Trim(),
            request.CheckInDate);

        if (isValid)
        {
            return Json(new
            {
                success = true,
                message = message,
                userName = endUser.Name,
                userImage = endUser.ImageUrl,
                subscriptionStartDate = endUser.SubscriptionStartDate.ToKSATime()
                    .ToString("MMM dd, yyyy"),
                subscriptionEndDate = endUser.SubscriptionEndDate.ToKSATime()
                    .ToString("MMM dd, yyyy"),
                phoneNumber = endUser.PhoneNumber
            });
        }

        return Json(new { success = false, message = message });
    }

    [HttpPost]
    public async Task<IActionResult> CreateManualCheckIn(
        [FromBody] AdminManualCheckInRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Json(new { success = false, message = "Invalid request data." });
        }

        // Validate required fields
        if (request.BranchId <= 0)
        {
            return Json(new { success = false, message = "Please select a valid branch." });
        }

        if (request.CheckInDateTime == default)
        {
            return Json(new { success = false, message = "Please provide a valid check-in date and time." });
        }

        // Create the manual check-in
        var result = await _checkInService.AdminManualCheckInAsync(
            request.PhoneNumber.Trim(),
            request.BranchId,
            request.CheckInDateTime,
            request.BranchCourtId,
            request.PlayDurationMinutes,
            request.PlayStartTime,
            request.Notes,
            request.PlayerAttended
        );

        return Json(new
        {
            success = result.Success,
            message = result.Message,
            checkInId = result.CheckInId
        });
    }
}

public class ValidateUserRequest
{
    public string PhoneNumber { get; set; }
    public DateTime CheckInDate { get; set; }
}