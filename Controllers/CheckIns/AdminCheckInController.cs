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
        if (request == null || request.CheckInId <= 0)
        {
            return Json(new { success = false, message = "Invalid check-in data." });
        }

        var checkIn = await _context.CheckIns
            .Include(c => c.EndUser)
            .FirstOrDefaultAsync(c => c.Id == request.CheckInId);

        if (checkIn == null)
        {
            return Json(new { success = false, message = "Check-in record not found." });
        }

        var checkInDateUtc = request.CheckInDate;

        var isThereCheckInWithSameDate =
            await _context.CheckIns.AnyAsync(x => x.CheckInDateTime.Date == checkInDateUtc.Date && x.Id  != checkIn.Id);

        if (isThereCheckInWithSameDate)
        {
            return Json(new { success = false, message = "There is check in in this date." });
        }

        // Convert play start time from KSA to UTC for storage
        DateTime? playStartTimeUtc = null;
        if (request.PlayStartTime.HasValue)
        {
            playStartTimeUtc = request.PlayStartTime.Value;
        }

        // Update check-in details
        checkIn.BranchCourtId = request.BranchCourtId;
        checkIn.PlayDuration = request.PlayDurationMinutes > 0
            ? TimeSpan.FromMinutes(request.PlayDurationMinutes)
            : null;
        checkIn.PlayStartTime = playStartTimeUtc;
        checkIn.Notes = !string.IsNullOrWhiteSpace(request.Notes) ? request.Notes.Trim() : null;
        checkIn.CheckInDateTime = checkInDateUtc;

        // Handle player attendance and warnings
        checkIn.PlayerAttended = request.PlayerAttended;
        var (isAutoStopped, warningMessage) =
            await _warningService.ProcessPlayerAttendanceAsync(request.CheckInId, request.PlayerAttended);

        try
        {
            await _context.SaveChangesAsync();

            var baseMessage = $"Check-in details updated successfully for {checkIn.EndUser.Name}";
            if (isAutoStopped)
            {
                return Json(new { success = true, message = $"{baseMessage}. {warningMessage}" });
            }
            else if (!request.PlayerAttended)
            {
                return Json(new { success = true, message = $"{baseMessage}. {warningMessage}" });
            }

            return Json(new { success = true, message = baseMessage });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while updating check-in details." });
        }
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