using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
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

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IQRCodeService qrCodeService,
            IExcelService excelService)
        {
            _context = context;
            _userManager = userManager;
            _qrCodeService = qrCodeService;
            _excelService = excelService;
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            var viewModel = new AdminDashboardViewModel
            {
                TotalBranches = _context.Branches.Count(),
                TotalEndUsers = _context.EndUsers.Count(),
                TotalCheckInsToday = _context.CheckIns.Count(c => c.CheckInDateTime.Date == DateTime.UtcNow.Date),
                ActiveSubscriptions = _context.EndUsers.Count(e =>
                    e.SubscriptionStartDate <= DateTime.UtcNow &&
                    e.SubscriptionEndDate >= DateTime.UtcNow)
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
                    IsActive = true
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
        public async Task<IActionResult> EndUsers()
        {
            var endUsers = await _context.EndUsers
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            return View(endUsers);
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

                var endUser = new EndUser
                {
                    Name = model.Name,
                    PhoneNumber = model.PhoneNumber,
                    Email = model.Email.ToLower(),
                    ImageUrl = model.ImageUrl,
                    SubscriptionStartDate = model.SubscriptionStartDate,
                    SubscriptionEndDate = model.SubscriptionEndDate,
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

            endUser.Name = model.Name;
            endUser.PhoneNumber = model.PhoneNumber;
            endUser.Email = model.Email;
            endUser.ImageUrl = model.ImageUrl;
            endUser.SubscriptionStartDate = model.SubscriptionStartDate;
            endUser.SubscriptionEndDate = model.SubscriptionEndDate;

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
            int? branchId)
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

            if (fromDate.HasValue)
            {
                query = query.Where(c => c.CheckInDateTime.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c => c.CheckInDateTime.Date <= toDate.Value.Date);
            }

            if (branchId.HasValue)
            {
                query = query.Where(c => c.BranchId == branchId.Value);
            }

            var checkIns = await query
                .OrderByDescending(c => c.CheckInDateTime)
                .ToListAsync();

            ViewBag.Branches = await _context.Branches.ToListAsync();
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.BranchId = branchId;

            return View(checkIns);
        }

        // Export to Excel
        [HttpGet]
        public async Task<IActionResult> ExportCheckIns(
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
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

            if (fromDate.HasValue)
            {
                query = query.Where(c => c.CheckInDateTime.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c => c.CheckInDateTime.Date <= toDate.Value.Date);
            }

            if (branchId.HasValue)
            {
                query = query.Where(c => c.BranchId == branchId.Value);
            }

            var checkIns = await query
                .OrderByDescending(c => c.CheckInDateTime)
                .ToListAsync();

            var excelData = _excelService.ExportCheckInsToExcel(checkIns);

            return File(excelData,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CheckIns_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // Add these new action methods to the existing AdminController class

// Subscription Pause Management
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
                CurrentSubscriptionEndDate = endUser.SubscriptionEndDate,
                PauseStartDate = DateTime.UtcNow.Date,
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

            var result = await pauseService.PauseSubscriptionAsync(
                model.EndUserId,
                model.PauseStartDate,
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
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
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
                TempData["Error"] = "Failed to reset password. " + string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("BranchUsers");
        }
    }
}

