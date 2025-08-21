namespace PadelPassCheckInSystem.Models.ViewModels.Dashboard;

public class DashboardAnalyticsViewModel
{
    // Basic stats
    public int TotalBranches { get; set; }
    public int TotalEndUsers { get; set; }
    public int TotalCheckInsToday { get; set; }
    public int ActiveSubscriptions { get; set; }

    // Advanced analytics
    public UserLoyaltySegmentsViewModel UserLoyaltySegments { get; set; }
    public DropoffAnalysisViewModel DropoffAnalysis { get; set; }
    public SubscriptionUtilizationViewModel SubscriptionUtilization { get; set; }
    public BranchPerformanceViewModel BranchPerformance { get; set; }
    public MultiBranchUsageViewModel MultiBranchUsage { get; set; }
    public CheckInTrendsViewModel CheckInTrends { get; set; }
    public BranchComparisonViewModel BranchComparison { get; set; }
}

public class UserLoyaltySegmentsViewModel
{
    public int VipUsers { get; set; }
    public int RegularUsers { get; set; }
    public int OccasionalUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalUsers { get; set; }
    public string AnalysisPeriod { get; set; }
    
    public List<UserWarningData> UsersWithWarnings { get; set; } = new();
}

public class UserWarningData
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string PhoneNumber { get; set; }
    public int WarningCount { get; set; }
    public bool IsStoppedByWarning { get; set; }
    public DateTime? StoppedDate { get; set; }
    public string Status { get; set; }
}

public class DropoffAnalysisViewModel
{
    public List<DropoffPeriodData> DropoffPeriods { get; set; } = new();
    public string AnalysisDate { get; set; }
}

public class DropoffPeriodData
{
    public string Period { get; set; }
    public int Days { get; set; }
    public int DroppedOffUsers { get; set; }
    public int TotalUsersWithHistory { get; set; }

    public double DropoffPercentage =>
        TotalUsersWithHistory > 0 ? (DroppedOffUsers / (double)TotalUsersWithHistory) * 100 : 0;
}

public class SubscriptionUtilizationViewModel
{
    public double AverageUtilization { get; set; }
    public int HighUtilizers { get; set; }
    public int LowUtilizers { get; set; }
    public int TotalUsers { get; set; }
    public List<UserUtilizationData> UserUtilizations { get; set; } = new();
}

public class UserUtilizationData
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public int TotalDays { get; set; }
    public int UsedDays { get; set; }
    public double UtilizationPercentage { get; set; }
}

public class BranchPerformanceViewModel
{
    public List<BranchPerformanceData> BranchPerformances { get; set; } = new();
    public string AnalysisPeriod { get; set; }
}

public class BranchPerformanceData
{
    public int BranchId { get; set; }
    public string BranchName { get; set; }
    public int TotalCheckIns { get; set; }
    public int CheckInsLast30Days { get; set; }
    public int TodayCheckIns { get; set; }
    public int UniqueUsersLast30Days { get; set; }
    public double AvgCheckInsPerDay { get; set; }
}

public class MultiBranchUsageViewModel
{
    public int SingleBranchUsers { get; set; }
    public int MultiBranchUsers { get; set; }
    public int MaxBranchesUsed { get; set; }
    public List<MultiBranchUserData> TopMultiBranchUsers { get; set; } = new();
    public int TotalUsersWithCheckIns { get; set; }
}

public class MultiBranchUserData
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public int BranchCount { get; set; }
    public int TotalCheckIns { get; set; }
}

public class CheckInTrendsViewModel
{
    public List<CheckInTrendData> Last7Days { get; set; } = new();
    public List<CheckInTrendData> Last30Days { get; set; } = new();
    public List<CheckInTrendData> Last90Days { get; set; } = new();
}

public class CheckInTrendData
{
    public DateTime Date { get; set; }
    public int CheckIns { get; set; }
    public string Label { get; set; }
}

public class BranchComparisonViewModel
{
    public List<BranchComparisonData> BranchComparisons { get; set; } = new();
    public string ComparisonPeriod { get; set; }
}

public class BranchComparisonData
{
    public int BranchId { get; set; }
    public string BranchName { get; set; }
    public int TotalCheckIns { get; set; }
    public int Last30DaysCheckIns { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueUsersLast30Days { get; set; }
    public int CourtAssignments { get; set; }
    public int PendingAssignments { get; set; }
    public double AvgDailyCheckIns { get; set; }
    public string PeakDayOfWeek { get; set; }
    public double CheckInScore { get; set; }
    public double UserEngagementScore { get; set; }
    public double CourtAssignmentRate { get; set; }
}