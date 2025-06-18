using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;

public class CheckInService : ICheckInService
{
    private readonly ApplicationDbContext _context;

    public CheckInService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message)> CheckInAsync(string identifier, int branchId)
    {
        // Find user by phone or unique identifier
        var endUser = await _context.EndUsers
            .FirstOrDefaultAsync(u => u.PhoneNumber == identifier || u.UniqueIdentifier == identifier);

        if (endUser == null)
        {
            return (false, "User not found");
        }

        // Check subscription validity
        var today = DateTime.UtcNow.Date;
        if (today < endUser.SubscriptionStartDate.Date || today > endUser.SubscriptionEndDate.Date)
        {
            return (false, "Subscription is not active");
        }

        // Check if already checked in today
        var hasCheckedInToday = await HasCheckedInTodayAsync(endUser.Id);
        if (hasCheckedInToday)
        {
            return (false, "User has already checked in today");
        }

        // Create check-in record
        var checkIn = new CheckIn
        {
            EndUserId = endUser.Id,
            BranchId = branchId,
            CheckInDateTime = DateTime.UtcNow
        };

        _context.CheckIns.Add(checkIn);
        await _context.SaveChangesAsync();

        return (true, $"Check-in successful for {endUser.Name}");
    }

    public async Task<bool> HasCheckedInTodayAsync(int endUserId)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.CheckIns
            .AnyAsync(c => c.EndUserId == endUserId && c.CheckInDateTime.Date == today);
    }
}