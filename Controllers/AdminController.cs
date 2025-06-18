using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers
{
    [Authorize(Roles = "Admin")]
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
        public async Task<IActionResult> Branches()
        {
            var branches = await _context.Branches
                .Include(b => b.BranchUsers)
                .OrderBy(b => b.Name)
                .ToListAsync();
            return View(branches);
        }

        [HttpPost]
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
        public async Task<IActionResult> UpdateBranchUser(string id, string fullName, string email, int? branchId)
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
        public async Task<IActionResult> DeleteBranchUser(string id)
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
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(BranchUsers));
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = user.LockoutEnd == null ? 
                DateTimeOffset.MaxValue : // Deactivate
                null; // Activate

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = $"User has been {(user.LockoutEnd == null ? "activated" : "deactivated")} successfully!";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(BranchUsers));
        }

        // End Users Management
        public async Task<IActionResult> EndUsers()
        {
            var endUsers = await _context.EndUsers
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            return View(endUsers);
        }

        [HttpPost]
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
        public async Task<IActionResult> GenerateQRCode(
            int endUserId)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null)
            {
                return NotFound();
            }

            var qrCodeBase64 = _qrCodeService.GenerateQRCodeBase64(endUser.UniqueIdentifier);

            return Json(new
            {
                success = true,
                qrCode = qrCodeBase64,
                identifier = endUser.UniqueIdentifier
            });
        }

        // Check-ins Management
        public async Task<IActionResult> CheckIns(
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
        {
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
        [HttpPost]
        public async Task<IActionResult> ExportCheckIns(
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
        {
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
    }
}
