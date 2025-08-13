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

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IQRCodeService qrCodeService,
            IExcelService excelService,
            ICheckInService checkInService,
            IPlaytomicSyncService playtomicSyncService,
            IPlaytomicIntegrationService playtomicIntegrationService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _qrCodeService = qrCodeService;
            _excelService = excelService;
            _checkInService = checkInService;
            _playtomicSyncService = playtomicSyncService;
            _playtomicIntegrationService = playtomicIntegrationService;
            _logger = logger;
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            // Use KSA time for dashboard statistics
            var currentDateKSA = KSADateTimeExtensions.GetKSANow()
                .Date;

            // Get all check-ins and filter by KSA date
            var allCheckIns = _context.CheckIns.ToList();
            var todayCheckInsKSA = allCheckIns.Count(c => c.CheckInDateTime.ToKSATime()
                .Date == currentDateKSA);

            // Get active subscriptions using KSA date
            var allEndUsers = _context.EndUsers.ToList();
            var activeSubscriptions = allEndUsers.Count(e =>
            {
                var startKSA = e.SubscriptionStartDate.ToKSATime()
                    .Date;
                var endKSA = e.SubscriptionEndDate.ToKSATime()
                    .Date;
                return startKSA <= currentDateKSA && endKSA >= currentDateKSA && !e.IsPaused;
            });

            var viewModel = new AdminDashboardViewModel
            {
                TotalBranches = _context.Branches.Count(),
                TotalEndUsers = _context.EndUsers.Count(),
                TotalCheckInsToday = todayCheckInsKSA,
                ActiveSubscriptions = activeSubscriptions
            };

            return View(viewModel);
        }

        // Branches Management
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Branches()
        {
            var branches = await _context.Branches
                .Include(b => b.BranchUsers)
                .OrderBy(b => b.Name)
                .ToListAsync();
            return View(branches);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateBranch(
            BranchViewModel model)
        {
            if (ModelState.IsValid)
            {
                var branch = new Branch
                {
                    Name = model.Name,
                    Address = model.Address,
                    IsActive = true,
                    PlaytomicTenantId = model.PlaytomicTenantId
                };

                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Branch created successfully!";
                return RedirectToAction(nameof(Branches));
            }

            return RedirectToAction(nameof(Branches));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBranch(
            int id,
            BranchViewModel model)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                branch.Name = model.Name;
                branch.Address = model.Address;
                branch.IsActive = model.IsActive;
                branch.PlaytomicTenantId = model.PlaytomicTenantId;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Branch updated successfully!";
            }

            return RedirectToAction(nameof(Branches));
        }

        // Branch Users Management
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BranchUsers()
        {
            var users = await _userManager.Users
                .Include(u => u.Branch)
                .ToListAsync();

            var branchUsers = new List<BranchUserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
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

            ViewBag.Branches = await _context.Branches.Where(b => b.IsActive)
                .ToListAsync();
            return View(branchUsers);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "BranchUser");
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBranchUser(
            string id,
            string fullName,
            string email,
            int? branchId)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(BranchUsers));
            }

            user.FullName = fullName;
            user.Email = email;
            user.UserName = email;
            user.BranchId = branchId;

            var result = await _userManager.UpdateAsync(user);
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBranchUser(
            string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(BranchUsers));
            }

            var result = await _userManager.DeleteAsync(user);
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleUserStatus(
            string id)
        {
            var user = await _userManager.FindByIdAsync(id);
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

            var result = await _userManager.UpdateAsync(user);
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

        // End Users Management
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EndUsers(
            string searchPhoneNumber,
            int page = 1,
            int pageSize = 10)
        {
            var query = _context.EndUsers.AsQueryable();

            // Apply phone number search filter
            if (!string.IsNullOrWhiteSpace(searchPhoneNumber))
            {
                searchPhoneNumber = searchPhoneNumber.Trim();
                query = query.Where(e => e.PhoneNumber.Contains(searchPhoneNumber));
            }

            // Order the query before pagination
            query = query.OrderByDescending(e => e.CreatedAt);

            // Get total count for pagination
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Apply pagination
            var endUsers = await query
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
                SearchPhoneNumber = searchPhoneNumber
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEndUser(
            EndUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if the end user already exists by phone number or email
                var existingUser = await _context.EndUsers
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

                _context.EndUsers.Add(endUser);
                await _context.SaveChangesAsync();

                TempData["Success"] = "End user created successfully!";
                return RedirectToAction(nameof(EndUsers));
            }

            return RedirectToAction(nameof(EndUsers));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEndUser(
            int id,
            EndUserViewModel model)
        {
            var endUser = await _context.EndUsers.FindAsync(id);

            if (endUser == null || !ModelState.IsValid) return RedirectToAction(nameof(EndUsers));

            if ((endUser.PhoneNumber != model.PhoneNumber &&
                 await _context.EndUsers.AnyAsync(e => e.PhoneNumber == model.PhoneNumber)) ||
                (endUser.Email != model.Email.ToLower() &&
                 await _context.EndUsers.AnyAsync(e => e.Email == model.Email.ToLower())))
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

            await _context.SaveChangesAsync();
            TempData["Success"] = "End user updated successfully!";

            return RedirectToAction(nameof(EndUsers));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEndUser(
            int id)
        {
            var endUser = await _context.EndUsers.FindAsync(id);

            if (endUser == null) return RedirectToAction(nameof(EndUsers));

            _context.EndUsers.Remove(endUser);
            await _context.SaveChangesAsync();
            TempData["Success"] = "End user deleted successfully!";

            return RedirectToAction(nameof(EndUsers));
        }

        // Generate QR Code
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateQRCode(
            int endUserId,
            bool forceRegenerate = false)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
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
            await _context.SaveChangesAsync();

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

// Branch Time Slots Management
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BranchTimeSlots(
            int? branchId)
        {
            var timeSlotService = HttpContext.RequestServices.GetRequiredService<IBranchTimeSlotService>();
            List<BranchTimeSlotViewModel> timeSlots;

            if (branchId.HasValue)
            {
                timeSlots = await timeSlotService.GetBranchTimeSlotsAsync(branchId.Value);
                var branch = await _context.Branches.FindAsync(branchId.Value);
                ViewBag.BranchName = branch?.Name;
            }
            else
            {
                timeSlots = await timeSlotService.GetAllTimeSlotsAsync();
            }

            ViewBag.BranchId = branchId;
            ViewBag.Branches = await _context.Branches.Where(b => b.IsActive)
                .ToListAsync();
            ViewBag.DaysOfWeek = Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .ToList();

            return View(timeSlots);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateTimeSlot(
            BranchTimeSlotViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid time slot data.";
                return RedirectToAction("BranchTimeSlots", new { branchId = model.BranchId });
            }

            if (!TimeSpan.TryParse(model.StartTime, out var startTime) ||
                !TimeSpan.TryParse(model.EndTime, out var endTime))
            {
                TempData["Error"] = "Invalid time format. Please use HH:mm format.";
                return RedirectToAction("BranchTimeSlots", new { branchId = model.BranchId });
            }

            var timeSlotService = HttpContext.RequestServices.GetRequiredService<IBranchTimeSlotService>();
            var result = await timeSlotService.AddTimeSlotAsync(model.BranchId, model.DayOfWeek, startTime, endTime);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("BranchTimeSlots", new { branchId = model.BranchId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTimeSlot(
            int id,
            string startTime,
            string endTime,
            bool isActive)
        {
            if (!TimeSpan.TryParse(startTime, out var parsedStartTime) ||
                !TimeSpan.TryParse(endTime, out var parsedEndTime))
            {
                TempData["Error"] = "Invalid time format. Please use HH:mm format.";
                return RedirectToAction("BranchTimeSlots");
            }

            var timeSlotService = HttpContext.RequestServices.GetRequiredService<IBranchTimeSlotService>();
            var result = await timeSlotService.UpdateTimeSlotAsync(id, parsedStartTime, parsedEndTime, isActive);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("BranchTimeSlots");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTimeSlot(
            int id)
        {
            var timeSlotService = HttpContext.RequestServices.GetRequiredService<IBranchTimeSlotService>();
            var result = await timeSlotService.DeleteTimeSlotAsync(id);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("BranchTimeSlots");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("BranchUsers");

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("BranchUsers");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

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

        // Stop Subscription Management
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> StopSubscription(
            StopSubscriptionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var endUser = await _context.EndUsers.FindAsync(model.EndUserId);
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
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Subscription for {endUser.Name} has been stopped successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping subscription for EndUser ID: {EndUserId}", model.EndUserId);
                TempData["Error"] = "An error occurred while stopping the subscription.";
            }

            return RedirectToAction("EndUsers");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReactivateSubscription(
            int endUserId)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
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

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Subscription for {endUser.Name} has been reactivated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reactivating subscription for EndUser ID: {EndUserId}", endUserId);
                TempData["Error"] = "An error occurred while reactivating the subscription.";
            }

            return RedirectToAction("EndUsers");
        }


        #region Playtomic Integration

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSyncPreview()
        {
            try
            {
                // Get active users count
                var todayKSA = KSADateTimeExtensions.GetKSANow()
                    .Date;
                var allUsers = await _context.EndUsers.ToListAsync();

                var activeUsersCount = allUsers.Count(user =>
                {
                    var startKSA = user.SubscriptionStartDate.ToKSATime()
                        .Date;
                    var endKSA = user.SubscriptionEndDate.ToKSATime()
                        .Date;
                    var isSubscriptionActive = startKSA <= todayKSA && endKSA >= todayKSA;
                    var isNotPaused = !user.IsPaused ||
                                      (user.CurrentPauseStartDate?.ToKSATime()
                                           .Date > todayKSA ||
                                       user.CurrentPauseEndDate?.ToKSATime()
                                           .Date < todayKSA);
                    return isSubscriptionActive && isNotPaused;
                });

                // Get branches with tenant ID count
                var branchesWithTenantCount = await _context.Branches
                    .CountAsync(b => b.PlaytomicTenantId.HasValue && b.IsActive);

                return Json(new
                {
                    success = true,
                    activeUsers = activeUsersCount,
                    branches = branchesWithTenantCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync preview: {Error}", ex.Message);
                return Json(new { success = false, message = "Error loading preview data." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncUsersToPlaytomic(
            [FromBody] SyncUsersToPlaytomicRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.AccessToken))
            {
                return Json(new { success = false, message = "Access token is required." });
            }

            try
            {
                var playtomicSyncService = HttpContext.RequestServices.GetRequiredService<IPlaytomicSyncService>();
                var result = await playtomicSyncService.SyncActiveUsersToPlaytomicAsync(request.AccessToken.Trim());

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Playtomic sync: {Error}", ex.Message);
                return Json(new { success = false, message = $"An error occurred during sync: {ex.Message}" });
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
                var integration = await _playtomicIntegrationService.GetActiveIntegrationAsync();
                
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
                _logger.LogError(ex, "Error getting Playtomic integration");
                return Json(new { success = false, message = "Error loading integration data" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SavePlaytomicIntegration([FromBody] PlaytomicIntegrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid integration data" });
            }

            try
            {
                var integration = await _playtomicIntegrationService.SaveIntegrationAsync(model);
                
                return Json(new { 
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
                _logger.LogError(ex, "Error saving Playtomic integration");
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
                var accessToken = await _playtomicIntegrationService.GetValidAccessTokenAsync();
                
                var result = await _playtomicSyncService.SyncActiveUsersToPlaytomicAsync(accessToken);

                if (result.IsSuccess)
                {
                    var message = $"Sync completed! Successfully synced {result.TotalUsers} users to {result.SuccessfulBranches}/{result.TotalBranches} branches.";

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
                _logger.LogError(ex, "Error during Playtomic sync with integration");
                return Json(new { success = false, message = $"An error occurred during sync: {ex.Message}" });
            }
        }
        
        
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncPlaytomicUserIds()
        {
            try
            {
                var updatedCount = await _playtomicIntegrationService.SyncCategoryMembersPlaytomicUserIdsAsync();

                return Json(new { success = true, updatedCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Playtomic integration");
                return Json(new { success = false, message = "Error loading integration data" });
            }
        }

        #endregion
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
