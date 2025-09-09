using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PadelPassCheckInSystem.Integration.Rekaz;
using PadelPassCheckInSystem.Models;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<HomeController> _logger;
        private readonly IEndUserSubscriptionService _endUserSubscriptionService;
        private readonly RekazClient _rekazClient;

        public HomeController(
            UserManager<ApplicationUser> userManager,
            ILogger<HomeController> logger,
            IEndUserSubscriptionService endUserSubscriptionService,
            RekazClient rekazClient)
        {
            _userManager = userManager;
            _logger = logger;
            _endUserSubscriptionService = endUserSubscriptionService;
            _rekazClient = rekazClient;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Admin"))
            {
                return RedirectToAction("Index", "Dashboard");
            }
            else if (roles.Contains("BranchUser"))
            {
                return RedirectToAction("Index", "CheckIn");
            }
            else if (roles.Contains("Finance"))
            {
                return RedirectToAction("CheckIns", "CheckIn");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        public async Task<IActionResult> Sync()
        {
            var subs = await _rekazClient.GetSubscriptionsAsync();
            foreach (var sub in subs.Items.GroupBy(x => x.CustomerId))
            {
                await _endUserSubscriptionService.SyncRekazAsync(sub);
            }

            return Ok();
        }
    }
}