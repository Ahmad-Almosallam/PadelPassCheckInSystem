using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels.Dashboard;

namespace PadelPassCheckInSystem.Services;

public interface IDashboardAnalyticsService
{
    Task<DashboardAnalyticsViewModel> GetDashboardAnalyticsAsync();

    Task<UserLoyaltySegmentsViewModel> GetUserLoyaltySegmentsAsync(
        IQueryable<EndUser> allUsers);

    Task<DropoffAnalysisViewModel> GetDropoffAnalysisAsync(
        IQueryable<EndUser> allUsers);

    Task<SubscriptionUtilizationViewModel> GetSubscriptionUtilizationAsync();
    Task<BranchPerformanceViewModel> GetBranchPerformanceAsync();
    Task<MultiBranchUsageViewModel> GetMultiBranchUsageAsync();
    Task<CheckInTrendsViewModel> GetCheckInTrendsAsync();
}

public class DashboardAnalyticsService : IDashboardAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public DashboardAnalyticsService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardAnalyticsViewModel> GetDashboardAnalyticsAsync()
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow()
            .Date;
        var startOfKSADayUtc = todayKSA.GetStartOfKSADayInUTC();
        var endOfKSADayUtc = todayKSA.GetEndOfKSADayInUTC();


        // Basic stats
        var totalBranches = await _context.Branches.CountAsync();
        var totalEndUsers = await _context.EndUsers.CountAsync();
        var totalCheckInsToday = await _context.CheckIns
            .CountAsync(c => c.CheckInDateTime >= startOfKSADayUtc && c.CheckInDateTime <= endOfKSADayUtc);

        var allUsers = await _context.EndUsers.ToListAsync();
        var activeSubscriptions = allUsers.Count(u =>
        {
            var startKSA = u.SubscriptionStartDate.ToKSATime()
                .Date;
            var endKSA = u.SubscriptionEndDate.ToKSATime()
                .Date;
            var isSubscriptionActive = startKSA <= todayKSA && endKSA >= todayKSA;
            var isNotPaused = !u.IsPaused ||
                              (u.CurrentPauseStartDate?.ToKSATime()
                                   .Date > todayKSA ||
                               u.CurrentPauseEndDate?.ToKSATime()
                                   .Date < todayKSA);
            return isSubscriptionActive && isNotPaused && !u.IsStopped;
        });

        return new DashboardAnalyticsViewModel
        {
            // Basic stats
            TotalBranches = totalBranches,
            TotalEndUsers = totalEndUsers,
            TotalCheckInsToday = totalCheckInsToday,
            ActiveSubscriptions = activeSubscriptions,

            // Advanced analytics
            UserLoyaltySegments = await GetUserLoyaltySegmentsAsync(allUsers.AsQueryable()),
            DropoffAnalysis = await GetDropoffAnalysisAsync(allUsers.AsQueryable()),
        };
    }

    #region User Loyalty

    public async Task<UserLoyaltySegmentsViewModel> GetUserLoyaltySegmentsAsync(
        IQueryable<EndUser> allUsers)
    {
        var last30DaysKSA = KSADateTimeExtensions.GetKSANow()
            .Date.AddDays(-30);
        var last30DaysUtcStart = last30DaysKSA.GetStartOfKSADayInUTC();
        var todayUtcEnd = KSADateTimeExtensions.GetKSANow()
            .Date.GetEndOfKSADayInUTC();
        var todayKSA = KSADateTimeExtensions.GetKSANow()
            .Date;

        var userCheckInCounts = await _context.CheckIns
            .Where(c => c.CheckInDateTime >= last30DaysUtcStart && c.CheckInDateTime <= todayUtcEnd)
            .GroupBy(c => c.EndUserId)
            .Select(g => new { UserId = g.Key, CheckInCount = g.Count() })
            .ToListAsync();

        var totalActiveUsers = allUsers
            .Where(SubscriptionPredicates.IsActiveOnDate(todayKSA))
            .Count();

        // Categorize users based on check-in frequency in last 30 days
        var vipUsers = userCheckInCounts.Count(u => u.CheckInCount >= 15); // 15+ check-ins
        var regularUsers = userCheckInCounts.Count(u => u.CheckInCount >= 5 && u.CheckInCount < 15); // 5-14 check-ins
        var occasionalUsers = userCheckInCounts.Count(u => u.CheckInCount >= 1 && u.CheckInCount < 5); // 1-4 check-ins
        var inactiveUsers = totalActiveUsers - userCheckInCounts.Count; // No check-ins

        return new UserLoyaltySegmentsViewModel
        {
            VipUsers = vipUsers,
            RegularUsers = regularUsers,
            OccasionalUsers = occasionalUsers,
            InactiveUsers = inactiveUsers,
            TotalUsers = totalActiveUsers,
            AnalysisPeriod = "Last 30 days"
        };
    }

    public async Task<DropoffAnalysisViewModel> GetDropoffAnalysisAsync(
        IQueryable<EndUser> allUsers)
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow()
            .Date;

        // KSA day starts in UTC for boundary comparisons
        var startTodayUtc = todayKSA.GetStartOfKSADayInUTC();
        var startMinus1Utc = todayKSA.AddDays(-1)
            .GetStartOfKSADayInUTC();
        var startMinus3Utc = todayKSA.AddDays(-3)
            .GetStartOfKSADayInUTC();
        var startMinus7Utc = todayKSA.AddDays(-7)
            .GetStartOfKSADayInUTC();
        var startMinus10Utc = todayKSA.AddDays(-10)
            .GetStartOfKSADayInUTC();

        // Active user ids (your subscription logic)
        var activeUserIds = allUsers
            .Where(SubscriptionPredicates.IsActiveOnDate(todayKSA))
            .Select(u => u.Id)
            .ToList();

        // Last check-in per active user (users with no check-ins are excluded by design)
        var lastCheckins = await _context.CheckIns
            .Where(ci => activeUserIds.Contains(ci.EndUserId))
            .GroupBy(ci => ci.EndUserId)
            .Select(g => new { EndUserId = g.Key, LastUtc = g.Max(x => x.CheckInDateTime) })
            .ToListAsync();

        // Bucket rules (exclusive, non-overlapping, based on KSA day starts)
        // 1 day:    [startMinus1, startToday)
        // 3 days:   [startMinus3, startMinus1)
        // 7 days:   [startMinus7, startMinus3)
        // 10 days:  [startMinus10, startMinus7)
        var b1 = lastCheckins.Count(x => x.LastUtc >= startMinus1Utc && x.LastUtc < startTodayUtc);
        var b3 = lastCheckins.Count(x => x.LastUtc >= startMinus3Utc && x.LastUtc < startMinus1Utc);
        var b7 = lastCheckins.Count(x => x.LastUtc >= startMinus7Utc && x.LastUtc < startMinus3Utc);
        var b10 = lastCheckins.Count(x => x.LastUtc >= startMinus10Utc && x.LastUtc < startMinus7Utc);

        // If you want to include active users who NEVER checked in, add them to the 10-day bucket:
        var neverChecked = activeUserIds.Count - lastCheckins.Count;
        b10 += Math.Max(neverChecked, 0);

        var totalWithHistory = lastCheckins.Count;

        var dropoffData = new List<DropoffPeriodData>
        {
            new DropoffPeriodData
                { Period = "1 days", Days = 1, DroppedOffUsers = b1, TotalUsersWithHistory = totalWithHistory },
            new DropoffPeriodData
                { Period = "3 days", Days = 3, DroppedOffUsers = b3, TotalUsersWithHistory = totalWithHistory },
            new DropoffPeriodData
                { Period = "7 days", Days = 7, DroppedOffUsers = b7, TotalUsersWithHistory = totalWithHistory },
            new DropoffPeriodData
                { Period = "10 days", Days = 10, DroppedOffUsers = b10, TotalUsersWithHistory = totalWithHistory },
        };

        return new DropoffAnalysisViewModel
        {
            DropoffPeriods = dropoffData,
            AnalysisDate = todayKSA.ToString("MMM dd, yyyy")
        };
    }

    #endregion

    #region Utilization

    public async Task<SubscriptionUtilizationViewModel> GetSubscriptionUtilizationAsync()
    {
        var todayKsa = KSADateTimeExtensions.GetKSANow()
            .Date;

        var activeUsersQuery =
            _context.EndUsers
                .Where(SubscriptionPredicates.IsActiveOnDate(todayKsa))
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.SubscriptionStartDate,
                    u.SubscriptionEndDate
                })
                .AsNoTracking();

        // 1) Project to (UserId, DayKey) for all check-ins inside each user's sub period.
        // DayKey is an int = number of days since a fixed epoch, fully translatable.
        var userDayKeysQuery =
            from u in activeUsersQuery
            join ci in _context.CheckIns.AsNoTracking() on u.Id equals ci.EndUserId
            // where ci.CheckInDateTime >= u.SubscriptionStartDate
            //       && ci.CheckInDateTime <= u.SubscriptionEndDate
            select new
            {
                u.Id,
            };

        // 2) Distinct days per user, then count per user (no nested GroupBy over groups).
        var usedDaysPerUserDict = await userDayKeysQuery
            .GroupBy(x => x.Id)
            .Select(g => new { UserId = g.Key, UsedDays = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.UsedDays);

        var users = await activeUsersQuery.ToListAsync();

        var utilizationData = users.Select(u =>
            {
                var start = u.SubscriptionStartDate;
                var end = u.SubscriptionEndDate;
                var totalDays = end >= start ? ((DateTime.UtcNow - start)).Days : 0;

                usedDaysPerUserDict.TryGetValue(u.Id, out var usedDays);

                var pct = totalDays > 0 ? (double)usedDays / totalDays * 100.0 : 0.0;

                return new UserUtilizationData
                {
                    UserId = u.Id,
                    UserName = u.Name,
                    TotalDays = totalDays,
                    UsedDays = usedDays,
                    UtilizationPercentage = pct
                };
            })
            .ToList();

        return new SubscriptionUtilizationViewModel
        {
            AverageUtilization = utilizationData.Any() ? utilizationData.Average(u => u.UtilizationPercentage) : 0.0,
            HighUtilizers = utilizationData.Count(u => u.UtilizationPercentage >= 80.0),
            LowUtilizers = utilizationData.Count(u => u.UtilizationPercentage < 20.0),
            TotalUsers = utilizationData.Count,
            UserUtilizations = utilizationData
                .OrderByDescending(u => u.UtilizationPercentage)
                .Take(10)
                .ToList()
        };
    }

    public async Task<MultiBranchUsageViewModel> GetMultiBranchUsageAsync()
    {
        // First, get all check-ins with user and branch data
        var userBranchUsage = await _context.CheckIns
            .Select(c => new { c.EndUserId, c.BranchId })
            .GroupBy(c => c.EndUserId)
            .Select(g => new
            {
                UserId = g.Key,
                BranchCount = g.Select(c => c.BranchId)
                    .Distinct()
                    .Count(),
                Branches = g.Select(c => c.BranchId)
                    .Distinct()
                    .ToList()
            })
            .ToListAsync();


        var singleBranchUsers = userBranchUsage.Count(u => u.BranchCount == 1);
        var multiBranchUsers = userBranchUsage.Count(u => u.BranchCount > 1);
        var maxBranches = userBranchUsage.Any() ? userBranchUsage.Max(u => u.BranchCount) : 0;

        // Get user IDs who use multiple branches
        var multiBranchUserIds = userBranchUsage
            .Where(ub => ub.BranchCount > 1)
            .Select(ub => ub.UserId)
            .ToList();

        // Get top multi-branch users with their total check-ins
        var topMultiBranchUsers = new List<MultiBranchUserData>();

        if (multiBranchUserIds.Any())
        {
            // Get user details and check-in counts separately
            var userDetails = await _context.EndUsers
                .Where(u => multiBranchUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();


            // Combine the data
            topMultiBranchUsers = userDetails
                .Select(u => new MultiBranchUserData
                {
                    UserId = u.Id,
                    UserName = u.Name,
                    BranchCount = userBranchUsage.First(ub => ub.UserId == u.Id)
                        .BranchCount,
                })
                .OrderByDescending(u => u.BranchCount)
                .Take(10)
                .ToList();
        }

        return new MultiBranchUsageViewModel
        {
            SingleBranchUsers = singleBranchUsers,
            MultiBranchUsers = multiBranchUsers,
            MaxBranchesUsed = maxBranches,
            TopMultiBranchUsers = topMultiBranchUsers,
            TotalUsersWithCheckIns = userBranchUsage.Count
        };
    }

    #endregion

    #region Trends

    public async Task<CheckInTrendsViewModel> GetCheckInTrendsAsync()
    {
        var last7 = await GetCheckInTrendDataDaily(7);
        var last30 = await GetCheckInTrendDataDaily(30);
        var last90W = await GetCheckInTrendDataWeekly(90);

        return new CheckInTrendsViewModel
        {
            Last7Days = last7,
            Last30Days = last30,
            Last90Days = last90W
        };
    }

// ---------- DAILY ----------
    private async Task<List<CheckInTrendData>> GetCheckInTrendDataDaily(
        int days)
    {
        var ksaToday = KSADateTimeExtensions.GetKSANow();
        var startUTC = ksaToday.AddDays(-days)
            .GetStartOfKSADayInUTC();
        var endUTC = ksaToday.AddDays(1)
            .GetEndOfKSADayInUTC();
        var result = new List<CheckInTrendData>();

        var checkInsGrouped = await _context.CheckIns
            .AsNoTracking()
            .Where(ci => ci.CheckInDateTime >= startUTC && ci.CheckInDateTime < endUTC)
            .Select(x => x.CheckInDateTime)
            .GroupBy(x => x.AddHours(3)
                .Date)
            .Select(x => new
            {
                x.Key,
                Count = x.Count()
            })
            .ToListAsync();

        foreach (var checkIn in checkInsGrouped)
        {
            result.Add(new CheckInTrendData
            {
                Date = checkIn.Key,
                CheckIns = checkIn.Count,
                Label = checkIn.Key.ToString("MMM dd")
            });
        }

        return result;
    }

// ---------- WEEKLY (KSA week starting Sunday) ----------
    private async Task<List<CheckInTrendData>> GetCheckInTrendDataWeekly(
        int days)
    {
        var ksaToday = KSADateTimeExtensions.GetKSANow();
        var startUTC = ksaToday.AddDays(-days)
            .GetStartOfKSADayInUTC();
        var endUTC = ksaToday.AddDays(1)
            .GetEndOfKSADayInUTC();

        var baseKsaMonday = new DateTime(2000, 1, 2); // Sunday

        var weekly = await _context.CheckIns
            .AsNoTracking()
            .Where(ci => ci.CheckInDateTime >= startUTC && ci.CheckInDateTime < endUTC)
            .Select(ci => ci.CheckInDateTime.AddHours(3)) // KSA local time (UTC+3, no DST)
            .GroupBy(local => EF.Functions.DateDiffWeek(baseKsaMonday, local))
            .Select(g => new
            {
                WeekIndex = g.Key,
                WeekStartKSA = baseKsaMonday.AddDays(g.Key * 7),
                Count = g.Count()
            })
            .OrderBy(x => x.WeekStartKSA)
            .ToListAsync();

        return weekly.Select(w => new CheckInTrendData
            {
                Date = w.WeekStartKSA,
                CheckIns = w.Count,
                Label = $"{w.WeekStartKSA:MMM dd} - {w.WeekStartKSA.AddDays(6):MMM dd}"
            })
            .ToList();
    }

    #endregion

    #region Branch Analytics

    public async Task<BranchPerformanceViewModel> GetBranchPerformanceAsync()
    {
        // Pre-calculate all date ranges once
        var ksaNow = KSADateTimeExtensions.GetKSANow()
            .Date;
        var last30DaysKSA = ksaNow.AddDays(-30);
        var last30DaysUtcStart = last30DaysKSA.GetStartOfKSADayInUTC();
        var todayUtcStart = ksaNow.GetStartOfKSADayInUTC();
        var todayUtcEnd = ksaNow.GetEndOfKSADayInUTC();

        // Single query to get all required data with proper grouping
        var branchStats = await _context.Branches
            .Where(b => b.IsActive)
            .GroupJoin(
                _context.CheckIns,
                branch => branch.Id,
                checkIn => checkIn.BranchId,
                (
                    branch,
                    checkIns) => new { Branch = branch, CheckIns = checkIns }
            )
            .Select(bg => new BranchPerformanceData
            {
                BranchId = bg.Branch.Id,
                BranchName = bg.Branch.Name,

                // Total check-ins for this branch
                TotalCheckIns = bg.CheckIns.Count(),

                // Check-ins in the last 30 days
                CheckInsLast30Days = bg.CheckIns
                    .Count(c => c.CheckInDateTime >= last30DaysUtcStart &&
                                c.CheckInDateTime <= todayUtcEnd),

                // Today's check-ins
                TodayCheckIns = bg.CheckIns
                    .Count(c => c.CheckInDateTime >= todayUtcStart &&
                                c.CheckInDateTime <= todayUtcEnd),

                // Unique users in the last 30 days
                UniqueUsersLast30Days = bg.CheckIns
                    .Where(c => c.CheckInDateTime >= last30DaysUtcStart &&
                                c.CheckInDateTime <= todayUtcEnd)
                    .Select(c => c.EndUserId)
                    .Distinct()
                    .Count(),

                // Average check-ins per day (last 30 days)
                AvgCheckInsPerDay = bg.CheckIns
                    .Count(c => c.CheckInDateTime >= last30DaysUtcStart &&
                                c.CheckInDateTime <= todayUtcEnd) / 30.0
            })
            .ToListAsync();

        return new BranchPerformanceViewModel
        {
            BranchPerformances = branchStats,
            AnalysisPeriod = "Last 30 days"
        };
    }

    #endregion
}

public class tempo
{
    public int BranchId { get; set; }
    public int EndUserId { get; set; }
    public DateTime CheckInDateTime { get; set; }
    public string CourtName { get; set; }
}