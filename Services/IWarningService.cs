using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;

namespace PadelPassCheckInSystem.Services;

public interface IWarningService
{
    Task<(bool IsAutoStopped, string Message)> ProcessPlayerAttendanceAsync(
        int checkInId,
        bool playerAttended);

    Task<(bool Success, string Message)> ReactivateWarningStoppedUserAsync(
        int endUserId);
}

public class WarningService : IWarningService
{
    private readonly ApplicationDbContext _context;

    public WarningService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool IsAutoStopped, string Message)> ProcessPlayerAttendanceAsync(
        int checkInId,
        bool playerAttended)
    {
        if (playerAttended) return (false, "Player attended - no warning issued");

        var checkIn = await _context.CheckIns
            .Include(c => c.EndUser)
            .FirstOrDefaultAsync(c => c.Id == checkInId);

        if (checkIn?.EndUser == null) return (false, "Check-in not found");

        var endUser = checkIn.EndUser;
        endUser.WarningCount++;

        var message = $"Warning #{endUser.WarningCount} issued to {endUser.Name}";

        if (endUser.WarningCount >= 3)
        {
            endUser.IsStopped = true;
            endUser.IsStoppedByWarning = true;
            endUser.StoppedDate = DateTime.UtcNow;
            endUser.StopReason = $"Automatically stopped after {endUser.WarningCount} warnings for non-attendance";

            message = $"{endUser.Name} has been automatically stopped after 3 warnings";
            await _context.SaveChangesAsync();
            return (true, message);
        }

        await _context.SaveChangesAsync();
        return (false, message);
    }

    public async Task<(bool Success, string Message)> ReactivateWarningStoppedUserAsync(
        int endUserId)
    {
        var endUser = await _context.EndUsers.FindAsync(endUserId);
        if (endUser == null) return (false, "User not found");

        if (!endUser.IsStoppedByWarning) return (false, "User was not stopped by warnings");

        endUser.IsStopped = false;
        endUser.IsStoppedByWarning = false;
        endUser.StoppedDate = null;
        endUser.StopReason = null;
        endUser.WarningCount = 0;

        await _context.SaveChangesAsync();
        return (true, $"User {endUser.Name} reactivated and warnings reset");
    }
}