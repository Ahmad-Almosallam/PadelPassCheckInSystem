using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.CheckIns
{
    [Authorize(Roles = "BranchUser,Admin")]
    public class CheckInController : CheckInBaseController
    {
        private readonly IExcelService _excelService;
        private readonly ApplicationDbContext _context;
        private readonly ICheckInService _checkInService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckInController(
            ICheckInService checkInService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IExcelService excelService) : base(excelService, context, checkInService, userManager)
        {
            _checkInService = checkInService;
            _userManager = userManager;
            _context = context;
            _excelService = excelService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                TempData["Error"] = "You are not assigned to any branch.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var todayCheckIns = await _checkInService.GetTodayCheckInsWithCourtInfoAsync(user.BranchId.Value);

            ViewBag.BranchName = (await _context.Branches.FindAsync(user.BranchId))?.Name;
            ViewBag.TodayCount = todayCheckIns.Count;

            return View(todayCheckIns);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCheckIn(
            [FromBody] ProcessCheckInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Identifier))
            {
                return Json(new { success = false, message = "Invalid barcode data." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                return Json(new { success = false, message = "You are not assigned to any branch." });
            }

            // Get the end user details for the confirmation message
            // var (isValid, message, endUser) =
            //     await _checkInService.ValidateCheckInAsync(request.Identifier, user.BranchId.Value);
            //
            // if (!isValid)
            // {
            //     return Json(new { success = false, message = message });
            // }
            
            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.Identifier || u.UniqueIdentifier == request.Identifier);

            // Return user details with default court assignment values
            return Json(new
            {
                success = true,
                userName = endUser.Name,
                userImage = endUser.ImageUrl,
                subEndDate = endUser.SubscriptionEndDate.ToString("d"),
                identifier = request.Identifier,
                requiresCourtAssignment = true,
                defaultPlayDurationMinutes = 90,
                defaultPlayStartTime = DateTime.Now.AddMinutes(5)
                    .ToString("HH:mm"),
                checkInTimeKSA = DateTime.UtcNow.ToKSATime()
                    .ToString("HH:mm:ss")
            });
        }
        
        [HttpGet]
        public async Task<IActionResult> GetRecentCheckIns()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                return Json(new { success = false });
            }

            // Use KSA date for filtering today's check-ins
            var todayKSA = KSADateTimeExtensions.GetKSAToday();

            var allCheckIns = await _context.CheckIns
                .Include(c => c.EndUser)
                .Include(x => x.BranchCourt)
                .Where(c => c.BranchId == user.BranchId)
                .OrderByDescending(c => c.CheckInDateTime)
                .Take(50) // Get more records to filter
                .ToListAsync();

            // Filter by KSA date and take top 10
            var recentCheckIns = allCheckIns
                .Where(c => c.CheckInDateTime.ToKSATime()
                    .Date == todayKSA)
                .Take(10)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.EndUser.Name,
                    time = c.CheckInDateTime.ToKSATime()
                        .ToString("HH:mm:ss"), // Convert to KSA time
                    image = c.EndUser.ImageUrl,
                    courtName = c.BranchCourt.CourtName,
                    playDuration = c.PlayDuration.HasValue
                        ? c.PlayDuration.Value.TotalMinutes.ToString("F0") + " min"
                        : "Not assigned",
                    playStartTime = c.PlayStartTime.HasValue
                        ? c.PlayStartTime.Value.ToKSATime()
                            .ToString("HH:mm") // Convert to KSA time
                        : "Not assigned",
                    hasCourtAssignment = !string.IsNullOrEmpty(c.CourtName)
                })
                .ToList();

            return Json(new { success = true, checkIns = recentCheckIns });
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteCheckIn(
            [FromBody] DeleteCheckInRequest request)
        {
            if (request == null || request.CheckInId <= 0)
            {
                return Json(new { success = false, message = "Invalid check-in ID." });
            }

            var user = await _userManager.GetUserAsync(User);

            // For non-admin users, verify branch access
            int? branchId = null;
            if (!User.IsInRole("Admin"))
            {
                if (user?.BranchId == null)
                {
                    return Json(new { success = false, message = "You are not assigned to any branch." });
                }

                branchId = user.BranchId;
            }

            var result = await _checkInService.DeleteCheckInAsync(request.CheckInId, branchId);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPhoneCheckIn(
            [FromBody] ProcessPhoneCheckInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.PhoneNumber))
            {
                return Json(new { success = false, message = "Invalid phone number." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                return Json(new { success = false, message = "You are not assigned to any branch." });
            }

            // Get the end user details for the confirmation message
            // var (isValid, message, endUser) =
            //     await _checkInService.ValidateCheckInAsync(request.PhoneNumber, user.BranchId.Value);
            //
            // if (!isValid)
            // {
            //     return Json(new { success = false, message = message });
            // }
            
            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

            // Return user details with default court assignment values
            return Json(new
            {
                success = true,
                userName = endUser.Name,
                userImage = endUser.ImageUrl,
                subEndDate = endUser.SubscriptionEndDate.ToString("d"),
                identifier = request.PhoneNumber,
                requiresCourtAssignment = true,
                defaultPlayDurationMinutes = 90,
                defaultPlayStartTime = DateTime.Now.AddMinutes(5)
                    .ToString("HH:mm"),
                checkInTimeKSA = DateTime.UtcNow.ToKSATime()
                    .ToString("HH:mm:ss")
            });
        }

        [HttpPost]
        public async Task<IActionResult> CheckInWithCourtAssignment(
            [FromBody] CheckInWithCourtAssignmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Identifier))
            {
                return Json(new { success = false, message = "Invalid identifier." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                return Json(new { success = false, message = "You are not assigned to any branch." });
            }

            // First, perform the check-in
            var checkInResult = await _checkInService.CheckInAsync(request.Identifier.Trim(), user.BranchId.Value,
                request.CheckInDate);
            if (!checkInResult.Success)
            {
                return Json(new { success = false, message = checkInResult.Message });
            }

            // Convert KSA play start time to UTC for court assignment
            DateTime? playStartTimeUtc = null;
            if (request.PlayStartTime != default(DateTime))
            {
                playStartTimeUtc = request.PlayStartTime;
            }

            // Then, assign the court
            var courtResult = await _checkInService.AssignCourtAsync(
                checkInResult.CheckInId.GetValueOrDefault(),
                request.BranchCourtId,
                request.PlayDurationMinutes,
                playStartTimeUtc, // Pass UTC time to service
                request.Notes,
                request.PlayerAttended
            );

            if (!courtResult.Success)
            {
                // If court assignment fails, we should still return the check-in info
                return Json(new
                {
                    success = true,
                    checkInSuccess = true,
                    courtAssignmentSuccess = false,
                    message = courtResult.Message,
                    checkInId = checkInResult.CheckInId
                });
            }

            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u =>
                    u.UniqueIdentifier == request.Identifier || u.PhoneNumber == request.Identifier);

            return Json(new
            {
                success = true,
                checkInSuccess = true,
                courtAssignmentSuccess = true,
                message = "Check-in completed and court assigned successfully",
                userName = endUser?.Name,
                userImage = endUser?.ImageUrl,
                checkInId = checkInResult.CheckInId,
                subEndDate = endUser?.SubscriptionEndDate.ToKSATime()
                    .ToString("d"), // Convert to KSA for display
                checkInTimeKSA = KSADateTimeExtensions.GetKSANow()
                    .ToString("HH:mm:ss"),
                courtName = request.BranchCourtId,
                playDurationMinutes = request.PlayDurationMinutes,
                playStartTime = request.PlayStartTime.ToString("HH:mm")
            });
        }
    }
}