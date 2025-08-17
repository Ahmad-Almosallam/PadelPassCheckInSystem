using System.Linq.Expressions;
using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Extensions;

public static class SubscriptionPredicates
{
    // Builds: Expression<Func<EndUser, bool>> for the given KSA date.
    // Usage:
    // var todayKsa = KSADateTimeExtensions.GetKSANow().Date;
    // var predicate = SubscriptionPredicates.IsActiveOnDate(todayKsa);
    // var count = await _context.EndUsers.CountAsync(predicate);
    public static Expression<Func<EndUser, bool>> IsActiveOnDate(DateTime ksaDate)
    {
        var startUtc = ksaDate.Date.GetStartOfKSADayInUTC();
        var endUtc = ksaDate.Date.GetEndOfKSADayInUTC();

        return u =>
            !u.IsStopped
            // Active during the KSA day (window overlap)
            && u.SubscriptionStartDate <= endUtc
            && u.SubscriptionEndDate >= startUtc
            // Not paused on that day
            && (
                !u.IsPaused
                || (u.CurrentPauseStartDate.HasValue && u.CurrentPauseStartDate > endUtc)
                || (u.CurrentPauseEndDate.HasValue && u.CurrentPauseEndDate < startUtc)
            );
    }
}