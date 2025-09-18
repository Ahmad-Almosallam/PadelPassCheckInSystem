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

public class CheckInBaseController : Controller
{
    private readonly IExcelService _excelService;
    private readonly ApplicationDbContext _context;
    private readonly ICheckInService _checkInService;
    private readonly UserManager<ApplicationUser> _userManager;


    public CheckInBaseController(
        IExcelService excelService,
        ApplicationDbContext context,
        ICheckInService checkInService,
        UserManager<ApplicationUser> userManager)
    {
        _excelService = excelService;
        _context = context;
        _checkInService = checkInService;
        _userManager = userManager;
    }

    [Authorize(Roles = "BranchUser,Admin,Finance")]
    public async Task<IActionResult> CheckIns(
        DateTime? fromDate,
        DateTime? toDate,
        int? branchId,
        string phoneNumber,
        int page = 1,
        int pageSize = 10)
    {
        // check if user is BranchUser and filter by branch
        if (User.IsInRole("BranchUser"))
        {
            var user = await _userManager.GetUserAsync(User);
            branchId ??= user.BranchId;
        }

        var query = FilterCheckInQuery(fromDate, toDate, branchId, phoneNumber);

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

        return View("~/Views/Admin/CheckIns.cshtml", viewModel);
    }

    

    [HttpGet]
    [Authorize(Roles = "BranchUser,Admin,Finance")]
    public async Task<IActionResult> ExportCheckIns(
        DateTime? fromDate,
        DateTime? toDate,
        int? branchId,
        string phoneNumber)
    {
        // check if user is BranchUser and filter by branch
        if (User.IsInRole("BranchUser"))
        {
            var user = await _userManager.GetUserAsync(User);
            branchId ??= user.BranchId;
        }
        
        var query = FilterCheckInQuery(fromDate, toDate, branchId, phoneNumber);

        var checkIns = await query
            .ToListAsync();

        var excelData = _excelService.ExportCheckInsToExcel(checkIns);

        return File(excelData,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"CheckIns_{KSADateTimeExtensions.GetKSANow():yyyyMMdd_HHmmss}_KSA.xlsx");
    }
    
    private IQueryable<CheckIn> FilterCheckInQuery(DateTime? fromDate, DateTime? toDate, int? branchId, string phoneNumber)
    {
        var query = _context.CheckIns
            .Include(c => c.EndUser)
            .Include(c => c.Branch)
            .Include(x => x.BranchCourt)
            .AsQueryable();
        
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
        query = query.OrderByDescending(c => c.CreatedAt);
        return query;
    }
    
    
}