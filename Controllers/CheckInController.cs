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

namespace PadelPassCheckInSystem.Controllers
{
    [Authorize(Roles = "BranchUser,Admin")]
    public class CheckInController : Controller
    {
        private readonly ICheckInService _checkInService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CheckInController(
            ICheckInService checkInService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _checkInService = checkInService;
            _userManager = userManager;
            _context = context;
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
            var pendingCourtAssignments = await _checkInService.GetPendingCourtAssignmentsAsync(user.BranchId.Value);

            ViewBag.BranchName = (await _context.Branches.FindAsync(user.BranchId))?.Name;
            ViewBag.TodayCount = todayCheckIns.Count;
            ViewBag.PendingAssignments = pendingCourtAssignments.Count;

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
            var (isValid, message, endUser) =
                await _checkInService.ValidateCheckInAsync(request.Identifier, user.BranchId.Value);

            if (!isValid)
            {
                return Json(new { success = false, message = message });
            }

            // Return user details with default court assignment values
            return Json(new
            {
                success = isValid,
                message = message,
                userName = endUser.Name,
                userImage = endUser.ImageUrl,
                subEndDate = endUser.SubscriptionEndDate.ToString("d"),
                identifier = request.Identifier,
                requiresCourtAssignment = true,
                defaultPlayDurationMinutes = 90,
                defaultPlayStartTime = DateTime.Now.AddMinutes(5).ToString("HH:mm"),
                checkInTimeKSA = DateTime.UtcNow.ToKSATime().ToString("HH:mm:ss")
            });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmCheckIn(
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

            var result = await _checkInService.CheckInAsync(request.Identifier.Trim(), user.BranchId.Value);

            if (result.Success)
            {
                // Get the end user details for the success message
                var endUser = await _context.EndUsers
                    .FirstOrDefaultAsync(u =>
                        u.UniqueIdentifier == request.Identifier || u.PhoneNumber == request.Identifier);

                return Json(new
                {
                    success = true,
                    message = result.Message,
                    userName = endUser?.Name,
                    userImage = endUser?.ImageUrl,
                    checkInId = result.CheckInId,
                    subEndDate = endUser?.SubscriptionEndDate.ToString("d"),
                    needsCourtAssignment = true,
                    checkInTimeKSA = DateTime.UtcNow.ToKSATime().ToString("HH:mm:ss")
                });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> AssignCourt([FromBody] AssignCourtRequest request)
        {
            if (request == null || request.CheckInId <= 0 || string.IsNullOrWhiteSpace(request.CourtName))
            {
                return Json(new { success = false, message = "Invalid court assignment data." });
            }

            // Convert KSA play start time to UTC if provided
            DateTime? playStartTimeUtc = null;
            if (request.PlayStartTime.HasValue)
            {
                playStartTimeUtc = request.PlayStartTime.Value;
            }

            var result = await _checkInService.AssignCourtAsync(
                request.CheckInId,
                request.CourtName,
                request.PlayDurationMinutes,
                playStartTimeUtc, // Pass UTC time to service
                request.Notes
            );

            return Json(new { success = result.Success, message = result.Message });
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
                .Where(c => c.BranchId == user.BranchId)
                .OrderByDescending(c => c.CheckInDateTime)
                .Take(50) // Get more records to filter
                .ToListAsync();

            // Filter by KSA date and take top 10
            var recentCheckIns = allCheckIns
                .Where(c => c.CheckInDateTime.ToKSATime().Date == todayKSA)
                .Take(10)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.EndUser.Name,
                    time = c.CheckInDateTime.ToKSATime().ToString("HH:mm:ss"), // Convert to KSA time
                    image = c.EndUser.ImageUrl,
                    courtName = c.CourtName,
                    playDuration = c.PlayDuration.HasValue
                        ? c.PlayDuration.Value.TotalMinutes.ToString("F0") + " min"
                        : "Not assigned",
                    playStartTime = c.PlayStartTime.HasValue
                        ? c.PlayStartTime.Value.ToKSATime().ToString("HH:mm") // Convert to KSA time
                        : "Not assigned",
                    hasCourtAssignment = !string.IsNullOrEmpty(c.CourtName)
                })
                .ToList();

            return Json(new { success = true, checkIns = recentCheckIns });
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingCourtAssignments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                return Json(new { success = false });
            }

            var pendingAssignments = await _checkInService.GetPendingCourtAssignmentsAsync(user.BranchId.Value);

            var result = pendingAssignments.Select(c => new
                {
                    id = c.Id,
                    name = c.EndUser.Name,
                    checkInTime = c.CheckInDateTime.ToKSATime().ToString("HH:mm:ss"), // Convert to KSA time
                    image = c.EndUser.ImageUrl,
                    phoneNumber = c.EndUser.PhoneNumber
                })
                .ToList();

            return Json(new { success = true, pendingAssignments = result });
        }

        [HttpGet]
        public async Task<IActionResult> CourtAssignment(int checkInId)
        {
            var checkIn = await _context.CheckIns
                .Include(c => c.EndUser)
                .Include(c => c.Branch)
                .FirstOrDefaultAsync(c => c.Id == checkInId);

            if (checkIn == null)
            {
                TempData["Error"] = "Check-in record not found.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != checkIn.BranchId)
            {
                TempData["Error"] = "You can only assign courts for your branch.";
                return RedirectToAction("Index");
            }

            if (!string.IsNullOrEmpty(checkIn.CourtName))
            {
                TempData["Error"] = "Court has already been assigned to this check-in.";
                return RedirectToAction("Index");
            }

            var viewModel = new CourtAssignmentViewModel
            {
                CheckInId = checkIn.Id,
                EndUserName = checkIn.EndUser.Name,
                CheckInDateTime = checkIn.CheckInDateTime.ToKSATime(), // Convert to KSA time for display
                BranchName = checkIn.Branch.Name,
                PlayDurationMinutes = 90, // Default 90 minutes
                PlayStartTime = KSADateTimeExtensions.GetKSANow().AddMinutes(5) // Default to 5 minutes from now in KSA
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CourtAssignment(CourtAssignmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Convert KSA play start time to UTC for storage
            DateTime? playStartTimeUtc = null;
            if (model.PlayStartTime.HasValue)
            {
                playStartTimeUtc = model.PlayStartTime.Value;
            }

            var result = await _checkInService.AssignCourtAsync(
                model.CheckInId,
                model.CourtName,
                model.PlayDurationMinutes,
                playStartTimeUtc, // Pass UTC time to service
                model.Notes
            );

            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction("Index");
            }
            else
            {
                TempData["Error"] = result.Message;
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCheckIn([FromBody] DeleteCheckInRequest request)
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
        public async Task<IActionResult> ProcessPhoneCheckIn([FromBody] ProcessPhoneCheckInRequest request)
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
            var (isValid, message, endUser) =
                await _checkInService.ValidateCheckInAsync(request.PhoneNumber, user.BranchId.Value);

            if (!isValid)
            {
                return Json(new { success = false, message = message });
            }

            // Return user details with default court assignment values
            return Json(new
            {
                success = isValid,
                message = message,
                userName = endUser.Name,
                userImage = endUser.ImageUrl,
                subEndDate = endUser.SubscriptionEndDate.ToString("d"),
                identifier = request.PhoneNumber,
                requiresCourtAssignment = true,
                defaultPlayDurationMinutes = 90,
                defaultPlayStartTime = DateTime.Now.AddMinutes(5).ToString("HH:mm"),
                checkInTimeKSA = DateTime.UtcNow.ToKSATime().ToString("HH:mm:ss")
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
            var checkInResult = await _checkInService.CheckInAsync(request.Identifier.Trim(), user.BranchId.Value);
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
                request.CourtName,
                request.PlayDurationMinutes,
                playStartTimeUtc, // Pass UTC time to service
                request.Notes
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
                subEndDate = endUser?.SubscriptionEndDate.ToKSATime().ToString("d"), // Convert to KSA for display
                checkInTimeKSA = KSADateTimeExtensions.GetKSANow().ToString("HH:mm:ss"),
                courtName = request.CourtName,
                playDurationMinutes = request.PlayDurationMinutes,
                playStartTime = request.PlayStartTime.ToString("HH:mm")
            });
        }
    }
}