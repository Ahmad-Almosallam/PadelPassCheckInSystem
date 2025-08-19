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
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IQRCodeService _qrCodeService;
        private readonly IExcelService _excelService;
        private readonly ICheckInService _checkInService;
        private readonly IPlaytomicSyncService _playtomicSyncService;
        private readonly IPlaytomicIntegrationService _playtomicIntegrationService;
        private readonly ILogger<AdminController> _logger;
        private readonly IDashboardAnalyticsService _dashboardAnalyticsService;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IQRCodeService qrCodeService,
            IExcelService excelService,
            ICheckInService checkInService,
            IPlaytomicSyncService playtomicSyncService,
            IPlaytomicIntegrationService playtomicIntegrationService,
            ILogger<AdminController> logger,
            IDashboardAnalyticsService dashboardAnalyticsService)
        {
            _context = context;
            _userManager = userManager;
            _qrCodeService = qrCodeService;
            _excelService = excelService;
            _checkInService = checkInService;
            _playtomicSyncService = playtomicSyncService;
            _playtomicIntegrationService = playtomicIntegrationService;
            _logger = logger;
            _dashboardAnalyticsService = dashboardAnalyticsService;
        }
        
        

        

        

        // Check-ins Management

        public async Task<IActionResult> CheckIns(
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId,
            string? phoneNumber,
            int page = 1,
            int pageSize = 10)
        {
            // check if user is BranchUser and filter by branch
            if (User.IsInRole("BranchUser"))
            {
                var user = await _userManager.GetUserAsync(User);
                branchId ??= user.BranchId;
            }

            var query = _context.CheckIns
                .Include(c => c.EndUser)
                .Include(c => c.Branch)
                .AsQueryable();

            // Convert date filters to UTC for database query

            if (fromDate.HasValue)
            {
                var fromDateUtc = fromDate.Value.ToUTCFromKSA();
                query = query.Where(c => c.CheckInDateTime >= fromDateUtc);
            }

            if (toDate.HasValue)
            {
                // Add one day and convert to get the end of the day in KSA
                var toDateUtc = toDate.Value.ToUTCFromKSA();
                query = query.Where(c => c.CheckInDateTime < toDateUtc);
            }

            if (branchId.HasValue)
            {
                query = query.Where(c => c.BranchId == branchId.Value);
            }

            // Add phone number filter
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                query = query.Where(c => c.EndUser.PhoneNumber.Contains(phoneNumber));
            }

            // Order the query before pagination
            query = query.OrderByDescending(c => c.CheckInDateTime);

            // Get total count for pagination
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Apply pagination
            var checkIns = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new CheckInsPaginatedViewModel
            {
                CheckIns = new PaginatedResult<CheckIn>
                {
                    Items = checkIns,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalItems = totalItems,
                    PageSize = pageSize
                },
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                PhoneNumber = phoneNumber,
                Branches = await _context.Branches.ToListAsync()
            };

            return View(viewModel);
        }

// Export to Excel with KSA time filtering
        [HttpGet]
        public async Task<IActionResult> ExportCheckIns(
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId,
            string? phoneNumber)
        {
            // check if user is BranchUser and filter by branch
            if (User.IsInRole("BranchUser"))
            {
                var user = await _userManager.GetUserAsync(User);
                branchId ??= user.BranchId;
            }

            var query = _context.CheckIns
                .Include(c => c.EndUser)
                .Include(c => c.Branch)
                .AsQueryable();

            // Convert date filters to UTC for database query
            if (fromDate.HasValue)
            {
                var fromDateUtc = fromDate.Value;
                query = query.Where(c => c.CheckInDateTime >= fromDateUtc);
            }

            if (toDate.HasValue)
            {
                // Add one day and convert to get the end of the day in KSA
                var toDateUtc = toDate.Value.AddDays(1);
                query = query.Where(c => c.CheckInDateTime < toDateUtc);
            }

            if (branchId.HasValue)
            {
                query = query.Where(c => c.BranchId == branchId.Value);
            }

            // Add phone number filter
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                query = query.Where(c => c.EndUser.PhoneNumber.Contains(phoneNumber));
            }

            var checkIns = await query
                .OrderByDescending(c => c.CheckInDateTime)
                .ToListAsync();

            var excelData = _excelService.ExportCheckInsToExcel(checkIns);

            return File(excelData,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CheckIns_{KSADateTimeExtensions.GetKSANow():yyyyMMdd_HHmmss}_KSA.xlsx");
        }

// Subscription Pause Management with KSA time validation
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PauseSubscription(
            int endUserId)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null)
            {
                TempData["Error"] = "End user not found.";
                return RedirectToAction("EndUsers");
            }

            if (endUser.IsPaused)
            {
                TempData["Error"] = "Subscription is already paused.";
                return RedirectToAction("EndUsers");
            }

            var viewModel = new PauseSubscriptionViewModel
            {
                EndUserId = endUserId,
                EndUserName = endUser.Name,
                CurrentSubscriptionEndDate = endUser.SubscriptionEndDate.ToKSATime(), // Convert to KSA for display
                PauseStartDate = KSADateTimeExtensions.GetKSANow()
                    .Date, // Use KSA date
                PauseDays = 7 // Default 7 days
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PauseSubscription(
            PauseSubscriptionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (DateTime.UtcNow.AddDays(2) > model.PauseStartDate)
            {
                TempData["Error"] = "Pause Start date must be two days from now";
                return RedirectToAction("EndUsers");
            }

            var pauseService = HttpContext.RequestServices.GetRequiredService<ISubscriptionPauseService>();
            var currentUserId = _userManager.GetUserId(User);

            // Note: PauseStartDate is already in KSA time from the form
            var result = await pauseService.PauseSubscriptionAsync(
                model.EndUserId,
                model.PauseStartDate, // This is in KSA time
                model.PauseDays,
                model.Reason,
                currentUserId
            );

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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnpauseSubscription(
            int endUserId)
        {
            var pauseService = HttpContext.RequestServices.GetRequiredService<ISubscriptionPauseService>();
            var currentUserId = _userManager.GetUserId(User);

            var result = await pauseService.UnpauseSubscriptionAsync(endUserId, currentUserId);

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

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PauseHistory(
            int? endUserId)
        {
            var pauseService = HttpContext.RequestServices.GetRequiredService<ISubscriptionPauseService>();
            List<SubscriptionPauseHistoryViewModel> pauseHistory;

            if (endUserId.HasValue)
            {
                pauseHistory = await pauseService.GetPauseHistoryAsync(endUserId.Value);
                var endUser = await _context.EndUsers.FindAsync(endUserId.Value);
                ViewBag.EndUserName = endUser?.Name;
            }
            else
            {
                pauseHistory = await pauseService.GetAllPauseHistoryAsync();
            }

            ViewBag.EndUserId = endUserId;
            return View(pauseHistory);
        }



        

        [Authorize(Roles = "Admin")]
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

            // Convert play start time from KSA to UTC for storage
            DateTime? playStartTimeUtc = null;
            if (request.PlayStartTime.HasValue)
            {
                playStartTimeUtc = request.PlayStartTime.Value;
            }

            // Update check-in details
            checkIn.CourtName = !string.IsNullOrWhiteSpace(request.CourtName) ? request.CourtName.Trim() : null;
            checkIn.PlayDuration = request.PlayDurationMinutes > 0
                ? TimeSpan.FromMinutes(request.PlayDurationMinutes)
                : null;
            checkIn.PlayStartTime = playStartTimeUtc;
            checkIn.Notes = !string.IsNullOrWhiteSpace(request.Notes) ? request.Notes.Trim() : null;

            try
            {
                await _context.SaveChangesAsync();
                return Json(new
                    { success = true, message = $"Check-in details updated successfully for {checkIn.EndUser.Name}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while updating check-in details." });
            }
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
                request.CourtName,
                request.PlayDurationMinutes,
                request.PlayStartTime,
                request.Notes
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

    public class SyncUsersToPlaytomicRequest
    {
        public string AccessToken { get; set; }
    }
}