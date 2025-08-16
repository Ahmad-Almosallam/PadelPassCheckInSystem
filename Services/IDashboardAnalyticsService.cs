using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.ViewModels.Dashboard;

namespace PadelPassCheckInSystem.Services;

public interface IDashboardAnalyticsService
{
    Task<DashboardAnalyticsViewModel> GetDashboardAnalyticsAsync();
    Task<UserLoyaltySegmentsViewModel> GetUserLoyaltySegmentsAsync();
    Task<DropoffAnalysisViewModel> GetDropoffAnalysisAsync();
    Task<SubscriptionUtilizationViewModel> GetSubscriptionUtilizationAsync();
    Task<BranchPerformanceViewModel> GetBranchPerformanceAsync();
    Task<MultiBranchUsageViewModel> GetMultiBranchUsageAsync();
    Task<CheckInTrendsViewModel> GetCheckInTrendsAsync();
    Task<BranchComparisonViewModel> GetBranchComparisonAsync();
}

public class DashboardAnalyticsService : IDashboardAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public DashboardAnalyticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardAnalyticsViewModel> GetDashboardAnalyticsAsync()
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow().Date;
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
            var startKSA = u.SubscriptionStartDate.ToKSATime().Date;
            var endKSA = u.SubscriptionEndDate.ToKSATime().Date;
            var isSubscriptionActive = startKSA <= todayKSA && endKSA >= todayKSA;
            var isNotPaused = !u.IsPaused ||
                              (u.CurrentPauseStartDate?.ToKSATime().Date > todayKSA ||
                               u.CurrentPauseEndDate?.ToKSATime().Date < todayKSA);
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
            UserLoyaltySegments = await GetUserLoyaltySegmentsAsync(),
            DropoffAnalysis = await GetDropoffAnalysisAsync(),
            SubscriptionUtilization = await GetSubscriptionUtilizationAsync(),
            BranchPerformance = await GetBranchPerformanceAsync(),
            MultiBranchUsage = await GetMultiBranchUsageAsync(),
            CheckInTrends = await GetCheckInTrendsAsync(),
            BranchComparison = await GetBranchComparisonAsync()
        };
    }

    public async Task<UserLoyaltySegmentsViewModel> GetUserLoyaltySegmentsAsync()
    {
        var last30DaysKSA = KSADateTimeExtensions.GetKSANow().Date.AddDays(-30);
        var last30DaysUtcStart = last30DaysKSA.GetStartOfKSADayInUTC();
        var todayUtcEnd = KSADateTimeExtensions.GetKSANow().Date.GetEndOfKSADayInUTC();

        var userCheckInCounts = await _context.CheckIns
            .Where(c => c.CheckInDateTime >= last30DaysUtcStart && c.CheckInDateTime <= todayUtcEnd)
            .GroupBy(c => c.EndUserId)
            .Select(g => new { UserId = g.Key, CheckInCount = g.Count() })
            .ToListAsync();

        var totalActiveUsers = await _context.EndUsers
            .Where(u => !u.IsStopped)
            .CountAsync();

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

    public async Task<DropoffAnalysisViewModel> GetDropoffAnalysisAsync()
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow().Date;
        var periods = new[]
        {
            new { Days = 7, Label = "7 days" },
            new { Days = 14, Label = "14 days" },
            new { Days = 30, Label = "30 days" },
            new { Days = 60, Label = "60 days" }
        };

        var dropoffData = new List<DropoffPeriodData>();

        foreach (var period in periods)
        {
            var cutoffDateKSA = todayKSA.AddDays(-period.Days);
            var cutoffDateUtc = cutoffDateKSA.GetEndOfKSADayInUTC();

            // Get users who have checked in before but not within the period
            var usersWithCheckIns = await _context.CheckIns
                .Where(c => c.CheckInDateTime < cutoffDateUtc)
                .Select(c => c.EndUserId)
                .Distinct()
                .ToListAsync();

            var usersWithRecentCheckIns = await _context.CheckIns
                .Where(c => c.CheckInDateTime >= cutoffDateUtc)
                .Select(c => c.EndUserId)
                .Distinct()
                .ToListAsync();

            var droppedOffUsers = usersWithCheckIns.Except(usersWithRecentCheckIns).Count();

            dropoffData.Add(new DropoffPeriodData
            {
                Period = period.Label,
                Days = period.Days,
                DroppedOffUsers = droppedOffUsers,
                TotalUsersWithHistory = usersWithCheckIns.Count
            });
        }

        return new DropoffAnalysisViewModel
        {
            DropoffPeriods = dropoffData,
            AnalysisDate = todayKSA.ToString("MMM dd, yyyy")
        };
    }

    public async Task<SubscriptionUtilizationViewModel> GetSubscriptionUtilizationAsync()
    {
        var allUsers = await _context.EndUsers
            .Where(u => !u.IsStopped)
            .ToListAsync();

        var utilizationData = new List<UserUtilizationData>();
        var todayKSA = KSADateTimeExtensions.GetKSANow().Date;

        foreach (var user in allUsers)
        {
            var subscriptionStartKSA = user.SubscriptionStartDate.ToKSATime().Date;
            var subscriptionEndKSA = user.SubscriptionEndDate.ToKSATime().Date;

            // Calculate actual subscription period
            var effectiveStartDate = subscriptionStartKSA > todayKSA ? todayKSA : subscriptionStartKSA;
            var effectiveEndDate = subscriptionEndKSA > todayKSA ? todayKSA : subscriptionEndKSA;

            if (effectiveEndDate < effectiveStartDate) continue;

            var totalDays = (effectiveEndDate - effectiveStartDate).Days + 1;

            // Get check-ins within subscription period
            var checkInDays = await _context.CheckIns
                .Where(c => c.EndUserId == user.Id)
                .ToListAsync();

            var checkInDaysInPeriod = checkInDays
                .Where(c => c.CheckInDateTime.ToKSATime().Date >= effectiveStartDate &&
                            c.CheckInDateTime.ToKSATime().Date <= effectiveEndDate)
                .Select(c => c.CheckInDateTime.ToKSATime().Date)
                .Distinct()
                .Count();

            var utilizationPercentage = totalDays > 0 ? (double)checkInDaysInPeriod / totalDays * 100 : 0;

            utilizationData.Add(new UserUtilizationData
            {
                UserId = user.Id,
                UserName = user.Name,
                TotalDays = totalDays,
                UsedDays = checkInDaysInPeriod,
                UtilizationPercentage = utilizationPercentage
            });
        }

        var averageUtilization = utilizationData.Any() ? utilizationData.Average(u => u.UtilizationPercentage) : 0;
        var highUtilizers = utilizationData.Count(u => u.UtilizationPercentage >= 80);
        var lowUtilizers = utilizationData.Count(u => u.UtilizationPercentage < 20);

        return new SubscriptionUtilizationViewModel
        {
            AverageUtilization = averageUtilization,
            HighUtilizers = highUtilizers,
            LowUtilizers = lowUtilizers,
            TotalUsers = utilizationData.Count,
            UserUtilizations = utilizationData.OrderByDescending(u => u.UtilizationPercentage).Take(10).ToList()
        };
    }

    public async Task<BranchPerformanceViewModel> GetBranchPerformanceAsync()
    {
        var last30DaysKSA = KSADateTimeExtensions.GetKSANow().Date.AddDays(-30);
        var last30DaysUtcStart = last30DaysKSA.GetStartOfKSADayInUTC();
        var todayUtcEnd = KSADateTimeExtensions.GetKSANow().Date.GetEndOfKSADayInUTC();

        var branchStats = await _context.Branches
            .Where(b => b.IsActive)
            .Select(b => new BranchPerformanceData
            {
                BranchId = b.Id,
                BranchName = b.Name,
                TotalCheckIns = _context.CheckIns.Count(c => c.BranchId == b.Id),
                CheckInsLast30Days = _context.CheckIns.Count(c => c.BranchId == b.Id &&
                                                                  c.CheckInDateTime >= last30DaysUtcStart &&
                                                                  c.CheckInDateTime <= todayUtcEnd),
                TodayCheckIns = _context.CheckIns.Count(c => c.BranchId == b.Id &&
                                                             c.CheckInDateTime >= KSADateTimeExtensions.GetKSANow().Date
                                                                 .GetStartOfKSADayInUTC() &&
                                                             c.CheckInDateTime <= KSADateTimeExtensions.GetKSANow().Date
                                                                 .GetEndOfKSADayInUTC()),
                UniqueUsersLast30Days = _context.CheckIns
                    .Where(c => c.BranchId == b.Id && c.CheckInDateTime >= last30DaysUtcStart &&
                                c.CheckInDateTime <= todayUtcEnd)
                    .Select(c => c.EndUserId)
                    .Distinct()
                    .Count(),
                AvgCheckInsPerDay = _context.CheckIns
                    .Where(c => c.BranchId == b.Id && c.CheckInDateTime >= last30DaysUtcStart &&
                                c.CheckInDateTime <= todayUtcEnd)
                    .Count() / 30.0
            })
            .ToListAsync();

        return new BranchPerformanceViewModel
        {
            BranchPerformances = branchStats,
            AnalysisPeriod = "Last 30 days"
        };
    }

    public async Task<MultiBranchUsageViewModel> GetMultiBranchUsageAsync()
    {
        // First, get all check-ins with user and branch data
        var checkInsData = await _context.CheckIns
            .Select(c => new { c.EndUserId, c.BranchId })
            .ToListAsync();

        // Group by user and count distinct branches (in-memory)
        var userBranchUsage = checkInsData
            .GroupBy(c => c.EndUserId)
            .Select(g => new
            {
                UserId = g.Key,
                BranchCount = g.Select(c => c.BranchId).Distinct().Count(),
                Branches = g.Select(c => c.BranchId).Distinct().ToList()
            })
            .ToList();

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

            var userCheckInCounts = checkInsData
                .Where(c => multiBranchUserIds.Contains(c.EndUserId))
                .GroupBy(c => c.EndUserId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Combine the data
            topMultiBranchUsers = userDetails
                .Select(u => new MultiBranchUserData
                {
                    UserId = u.Id,
                    UserName = u.Name,
                    BranchCount = userBranchUsage.First(ub => ub.UserId == u.Id).BranchCount,
                    TotalCheckIns = userCheckInCounts.GetValueOrDefault(u.Id, 0)
                })
                .OrderByDescending(u => u.BranchCount)
                .ThenByDescending(u => u.TotalCheckIns)
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

    public async Task<CheckInTrendsViewModel> GetCheckInTrendsAsync()
    {
        var todayKSA = KSADateTimeExtensions.GetKSANow().Date;

        // 7-day trend
        var last7Days = await GetCheckInTrendData(7);

        // 30-day trend
        var last30Days = await GetCheckInTrendData(30);

        // 90-day trend (weekly aggregation)
        var last90Days = await GetCheckInTrendDataWeekly(90);

        return new CheckInTrendsViewModel
        {
            Last7Days = last7Days,
            Last30Days = last30Days,
            Last90Days = last90Days
        };
    }

    private async Task<List<CheckInTrendData>> GetCheckInTrendData(int days)
    {
        var endDate = KSADateTimeExtensions.GetKSANow().Date;
        var startDate = endDate.AddDays(-days);

        var trendData = new List<CheckInTrendData>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dayStartUtc = date.GetStartOfKSADayInUTC();
            var dayEndUtc = date.GetEndOfKSADayInUTC();

            var checkIns = await _context.CheckIns
                .CountAsync(c => c.CheckInDateTime >= dayStartUtc && c.CheckInDateTime <= dayEndUtc);

            var uniqueUsers = await _context.CheckIns
                .Where(c => c.CheckInDateTime >= dayStartUtc && c.CheckInDateTime <= dayEndUtc)
                .Select(c => c.EndUserId)
                .Distinct()
                .CountAsync();

            trendData.Add(new CheckInTrendData
            {
                Date = date,
                CheckIns = checkIns,
                UniqueUsers = uniqueUsers,
                Label = date.ToString("MMM dd")
            });
        }

        return trendData;
    }

    private async Task<List<CheckInTrendData>> GetCheckInTrendDataWeekly(int days)
    {
        var endDate = KSADateTimeExtensions.GetKSANow().Date;
        var startDate = endDate.AddDays(-days);

        var trendData = new List<CheckInTrendData>();

        // Group by weeks
        var currentWeekStart = startDate.AddDays(-(int)startDate.DayOfWeek);

        while (currentWeekStart <= endDate)
        {
            var weekEnd = currentWeekStart.AddDays(6);
            if (weekEnd > endDate) weekEnd = endDate;

            var weekStartUtc = currentWeekStart.GetStartOfKSADayInUTC();
            var weekEndUtc = weekEnd.GetEndOfKSADayInUTC();

            var checkIns = await _context.CheckIns
                .CountAsync(c => c.CheckInDateTime >= weekStartUtc && c.CheckInDateTime <= weekEndUtc);

            var uniqueUsers = await _context.CheckIns
                .Where(c => c.CheckInDateTime >= weekStartUtc && c.CheckInDateTime <= weekEndUtc)
                .Select(c => c.EndUserId)
                .Distinct()
                .CountAsync();

            trendData.Add(new CheckInTrendData
            {
                Date = currentWeekStart,
                CheckIns = checkIns,
                UniqueUsers = uniqueUsers,
                Label = $"{currentWeekStart:MMM dd} - {weekEnd:MMM dd}"
            });

            currentWeekStart = currentWeekStart.AddDays(7);
        }

        return trendData;
    }

    public async Task<BranchComparisonViewModel> GetBranchComparisonAsync()
    {
        var last30DaysKSA = KSADateTimeExtensions.GetKSANow().Date.AddDays(-30);
        var last30DaysUtcStart = last30DaysKSA.GetStartOfKSADayInUTC();
        var todayUtcEnd = KSADateTimeExtensions.GetKSANow().Date.GetEndOfKSADayInUTC();

        // Get branch basic data first
        var branches = await _context.Branches
            .Where(b => b.IsActive)
            .Select(b => new { b.Id, b.Name })
            .ToListAsync();

        // Get all check-ins for the last 30 days
        var checkInsLast30Days = await _context.CheckIns
            .Where(c => c.CheckInDateTime >= last30DaysUtcStart && c.CheckInDateTime <= todayUtcEnd)
            .Select(c => new tempo
            {
                BranchId = c.BranchId,
                EndUserId = c.EndUserId,
                CheckInDateTime = c.CheckInDateTime,
                CourtName = c.CourtName
            })
            .ToListAsync();

        // Get all check-ins for total counts
        var allCheckIns = await _context.CheckIns
            .Select(c => new { c.BranchId, c.EndUserId, c.CourtName })
            .ToListAsync();

        var branchComparisons = new List<BranchComparisonData>();

        foreach (var branch in branches)
        {
            // Filter check-ins for this branch
            var branchCheckInsLast30Days = checkInsLast30Days.Where(c => c.BranchId == branch.Id).ToList();
            var branchAllCheckIns = allCheckIns.Where(c => c.BranchId == branch.Id).ToList();

            // Calculate metrics
            var totalCheckIns = branchAllCheckIns.Count;
            var last30DaysCheckIns = branchCheckInsLast30Days.Count;
            var uniqueUsers = branchAllCheckIns.Select(c => c.EndUserId).Distinct().Count();
            var uniqueUsersLast30Days = branchCheckInsLast30Days.Select(c => c.EndUserId).Distinct().Count();
            var courtAssignments = branchAllCheckIns.Count(c => !string.IsNullOrEmpty(c.CourtName));
            var pendingAssignments = branchAllCheckIns.Count(c => string.IsNullOrEmpty(c.CourtName));
            var avgDailyCheckIns = last30DaysCheckIns / 30.0;

            // Calculate peak day of week
            var peakDayOfWeek = GetPeakDayOfWeekForBranch(branchCheckInsLast30Days);

            branchComparisons.Add(new BranchComparisonData
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                TotalCheckIns = totalCheckIns,
                Last30DaysCheckIns = last30DaysCheckIns,
                UniqueUsers = uniqueUsers,
                UniqueUsersLast30Days = uniqueUsersLast30Days,
                CourtAssignments = courtAssignments,
                PendingAssignments = pendingAssignments,
                AvgDailyCheckIns = avgDailyCheckIns,
                PeakDayOfWeek = peakDayOfWeek
            });
        }

        // Calculate relative performance scores
        if (branchComparisons.Any())
        {
            var maxCheckIns = branchComparisons.Max(b => b.Last30DaysCheckIns);
            var maxUsers = branchComparisons.Max(b => b.UniqueUsersLast30Days);

            foreach (var branch in branchComparisons)
            {
                branch.CheckInScore = maxCheckIns > 0 ? (branch.Last30DaysCheckIns / (double)maxCheckIns) * 100 : 0;
                branch.UserEngagementScore = maxUsers > 0 ? (branch.UniqueUsersLast30Days / (double)maxUsers) * 100 : 0;
                branch.CourtAssignmentRate = branch.TotalCheckIns > 0
                    ? (branch.CourtAssignments / (double)branch.TotalCheckIns) * 100
                    : 0;
            }
        }

        return new BranchComparisonViewModel
        {
            BranchComparisons = branchComparisons.OrderByDescending(b => b.Last30DaysCheckIns).ToList(),
            ComparisonPeriod = "Last 30 days"
        };
    }

// Make this method static to avoid the memory leak warning
    private static string GetPeakDayOfWeekForBranch(List<tempo> branchCheckIns)
    {
        try
        {
            if (!branchCheckIns.Any()) return "N/A";

            var dayGroups = branchCheckIns
                .GroupBy(c => ((DateTime)c.CheckInDateTime).ToKSATime().DayOfWeek)
                .Select(g => new { DayOfWeek = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .FirstOrDefault();

            return dayGroups?.DayOfWeek.ToString() ?? "N/A";
        }
        catch
        {
            return "N/A";
        }
    }
}

public class tempo
{
    public int BranchId { get; set; }
    public int EndUserId { get; set; }
    public DateTime CheckInDateTime { get; set; }
    public string CourtName { get; set; }
}