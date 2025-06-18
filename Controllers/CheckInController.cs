using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers
{
    [Authorize(Roles = "BranchUser")]
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

            var todayCheckIns = await _context.CheckIns
                .Include(c => c.EndUser)
                .Where(c => c.BranchId == user.BranchId && c.CheckInDateTime.Date == DateTime.UtcNow.Date)
                .OrderByDescending(c => c.CheckInDateTime)
                .ToListAsync();

            ViewBag.BranchName = (await _context.Branches.FindAsync(user.BranchId))?.Name;
            ViewBag.TodayCount = todayCheckIns.Count;

            return View(todayCheckIns);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCheckIn([FromBody] ProcessCheckInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Identifier))
            {
                return Json(new { success = false, message = "Please enter a phone number or scan a QR code." });
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
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.Identifier || u.UniqueIdentifier == request.Identifier);
                
                return Json(new { 
                    success = true, 
                    message = result.Message,
                    userName = endUser?.Name,
                    userImage = endUser?.ImageUrl
                });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentCheckIns()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId == null)
            {
                return Json(new { success = false });
            }

            var recentCheckIns = await _context.CheckIns
                .Include(c => c.EndUser)
                .Where(c => c.BranchId == user.BranchId && c.CheckInDateTime.Date == DateTime.UtcNow.Date)
                .OrderByDescending(c => c.CheckInDateTime)
                .Take(5)
                .Select(c => new
                {
                    name = c.EndUser.Name,
                    time = c.CheckInDateTime.ToLocalTime().ToString("HH:mm:ss"),
                    image = c.EndUser.ImageUrl
                })
                .ToListAsync();

            return Json(new { success = true, checkIns = recentCheckIns });
        }
    }

    public class ProcessCheckInRequest
    {
        public string Identifier { get; set; }
    }
}
