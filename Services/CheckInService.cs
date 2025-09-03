using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NodaTime.Extensions;
using PadelPassCheckInSystem.Controllers.CheckIns;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Services;

public class CheckInService : ICheckInService
{
    private readonly ApplicationDbContext _context;
    private readonly IWarningService _warningService;

    public CheckInService(
        ApplicationDbContext context,
        IWarningService warningService)
    {
        _context = context;
        _warningService = warningService;
    }

    public async Task<(bool Success, string Message, int? CheckInId)> CheckInAsync(
        string identifier,
        int branchId,
        DateTime requestCheckInDateUtc)
    {
        var (isValid, message, endUser) = await ValidateCheckInAsync(identifier, branchId, requestCheckInDateUtc);

        if (!isValid)
        {
            return (false, message, null);
        }

        // Create check-in record
        var checkIn = new CheckIn
        {
            EndUserId = endUser.Id,
            BranchId = branchId,
            CheckInDateTime = requestCheckInDateUtc
        };

        _context.CheckIns.Add(checkIn);
        await _context.SaveChangesAsync();

        return (true, $"Check-in successful for {endUser.Name}", checkIn.Id);
    }

    public async Task<(bool Success, string Message)> AssignCourtAsync(
        int checkInId,
        int branchCourtId,
        int playDurationMinutes,
        DateTime? playStartTime,
        string notes,
        bool playerAttended = true)
    {
        var checkIn = await _context.CheckIns
            .Include(c => c.EndUser)
            .FirstOrDefaultAsync(c => c.Id == checkInId);

        if (checkIn == null)
        {
            return (false, "Check-in record not found");
        }

        if (!string.IsNullOrEmpty(checkIn.CourtName))
        {
            return (false, "Court has already been assigned to this check-in");
        }

        DateTime? playStartTimeUtc = null;
        if (playStartTime.HasValue)
        {
            playStartTimeUtc = playStartTime.Value;
        }

        // Update check-in with court assignment

        checkIn.BranchCourtId = branchCourtId;
        checkIn.PlayDuration = TimeSpan.FromMinutes(playDurationMinutes);
        checkIn.PlayStartTime = playStartTimeUtc ?? DateTime.UtcNow;
        checkIn.Notes = notes.Trim()
            .IsNullOrEmpty()
            ? null
            : notes.Trim();
        checkIn.PlayerAttended = playerAttended;

        await _context.SaveChangesAsync();

        var baseMessage = $"Court '{branchCourtId}' assigned successfully to {checkIn.EndUser.Name}";

        // Process warning if player didn't attend
        var (isAutoStopped, warningMessage) =
            await _warningService.ProcessPlayerAttendanceAsync(checkInId, playerAttended);

        if (isAutoStopped)
        {
            return (true, $"{baseMessage}. {warningMessage}");
        }
        else if (!playerAttended)
        {
            return (true, $"{baseMessage}. {warningMessage}");
        }

        return (true, baseMessage);
    }

    public async Task<(bool Success, string Message)> DeleteCheckInAsync(
        int checkInId,
        int? branchId = null)
    {
        var checkIn = await _context.CheckIns
            .Include(c => c.EndUser)
            .Include(c => c.Branch)
            .FirstOrDefaultAsync(c => c.Id == checkInId);

        if (checkIn == null)
        {
            return (false, "Check-in record not found");
        }

        // If branchId is provided (non-admin user), verify branch access and date
        if (branchId.HasValue)
        {
            if (checkIn.BranchId != branchId.Value)
            {
                return (false, "You can only delete check-ins from your branch");
            }

            // Only non-admin users are restricted to deleting today's check-ins (KSA time)
            var todayKSA = KSADateTimeExtensions.GetKSANow()
                .Date;
            var checkInDateKSA = checkIn.CheckInDateTime.ToKSATime()
                .Date;
            if (checkInDateKSA != todayKSA)
            {
                return (false, "You can only delete today's check-ins");
            }
        }

        var userName = checkIn.EndUser.Name;

        _context.CheckIns.Remove(checkIn);
        await _context.SaveChangesAsync();

        return (true, $"Check-in for {userName} has been deleted successfully");
    }

    public async Task<bool> HasCheckedInTodayAsync(
        int endUserId)
    {
        // Use KSA date for comparison
        var todayKSA = KSADateTimeExtensions.GetKSANow()
            .Date;

        // Get all check-ins for this user and convert to KSA time for comparison
        var userCheckIns = await _context.CheckIns
            .Where(c => c.EndUserId == endUserId)
            .ToListAsync();

        return userCheckIns.Any(c => c.CheckInDateTime.ToKSATime()
            .Date == todayKSA);
    }

    private async Task<bool> IsWithinAllowedTimeSlotAsync(
        int branchId,
        DayOfWeek dayOfWeek,
        TimeSpan currentTime)
    {
        var timeSlots = await _context.BranchTimeSlots
            .Where(ts => ts.BranchId == branchId &&
                         ts.DayOfWeek == dayOfWeek &&
                         ts.IsActive)
            .ToListAsync();

        if (!timeSlots.Any())
        {
            // If no time slots configured, allow check-in anytime
            return true;
        }

        foreach (var slot in timeSlots)
        {
            // Handle time slots that cross midnight (e.g., 22:00 to 04:00)
            if (slot.StartTime <= slot.EndTime)
            {
                // Normal time slot (doesn't cross midnight)
                if (currentTime >= slot.StartTime && currentTime <= slot.EndTime)
                {
                    return true;
                }
            }
            else
            {
                // Time slot crosses midnight
                if (currentTime >= slot.StartTime || currentTime <= slot.EndTime)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<string> GetBranchTimeSlotDisplayAsync(
        int branchId,
        DayOfWeek dayOfWeek)
    {
        var timeSlots = await _context.BranchTimeSlots
            .Where(ts => ts.BranchId == branchId &&
                         ts.DayOfWeek == dayOfWeek &&
                         ts.IsActive)
            .OrderBy(ts => ts.StartTime)
            .ToListAsync();

        if (!timeSlots.Any())
        {
            return "No specific time restrictions";
        }

        var timeRanges = timeSlots.Select(ts =>
            $"{ts.StartTime:hh\\:mm} - {ts.EndTime:hh\\:mm}");

        return string.Join(", ", timeRanges);
    }

    private async Task<DateTime> GetEffectiveSubscriptionEndDateAsync(
        int endUserId)
    {
        var endUser = await _context.EndUsers.FindAsync(endUserId);
        if (endUser == null)
        {
            return DateTime.MinValue;
        }

        // Calculate total pause days from all completed pauses
        var totalPauseDays = await _context.SubscriptionPauses
            .Where(sp => sp.EndUserId == endUserId && !sp.IsActive)
            .SumAsync(sp => sp.PauseDays);

        // Add pause days to the original subscription end date
        return endUser.SubscriptionEndDate.AddDays(totalPauseDays);
    }

    private async Task UnpauseSubscriptionAsync(
        int endUserId)
    {
        var endUser = await _context.EndUsers.FindAsync(endUserId);
        if (endUser != null && endUser.IsPaused)
        {
            endUser.IsPaused = false;
            endUser.CurrentPauseStartDate = null;
            endUser.CurrentPauseEndDate = null;

            // Mark current active pause as completed
            var activePause = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUserId && sp.IsActive);

            if (activePause != null)
            {
                activePause.IsActive = false;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<CheckIn>> GetPendingCourtAssignmentsAsync(
        int branchId)
    {
        // Use KSA date for filtering today's check-ins
        var todayKSA = KSADateTimeExtensions.GetKSANow()
            .Date;

        var allCheckIns = await _context.CheckIns
            .Include(c => c.EndUser)
            .Where(c => c.BranchId == branchId && string.IsNullOrEmpty(c.CourtName))
            .ToListAsync();

        // Filter by KSA date
        return allCheckIns
            .Where(c => c.CheckInDateTime.ToKSATime()
                .Date == todayKSA)
            .OrderBy(c => c.CheckInDateTime)
            .ToList();
    }

    public async Task<List<CheckIn>> GetTodayCheckInsWithCourtInfoAsync(int branchId)
    {
        // 1) Get branch TZ
        var tz = await _context.Branches
            .Where(b => b.Id == branchId)
            .Select(b => b.TimeZoneId)
            .SingleOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(tz))
            return new List<CheckIn>(); // or throw/handle as you prefer

        // 2) Compute branch-local "today" and its UTC window
        var todayLocal = NodaTimeExtensions.GetLocalNow(tz).Date;
        var startUtc = todayLocal.GetStartOfDayUtc(tz);
        var endUtc   = todayLocal.GetEndOfDayUtc(tz);

        // 3) Query in UTC (efficient) + include related info
        return await _context.CheckIns
            .Include(c => c.EndUser)
            .Include(c => c.BranchCourt)
            .Where(c => c.BranchId == branchId &&
                        c.CheckInDateTime >= startUtc &&
                        c.CheckInDateTime <= endUtc)
            .OrderByDescending(c => c.CheckInDateTime)
            .ToListAsync();
    }


    public async Task<(bool IsValid, string Message, EndUser User)> ValidateCheckInAsync(
        string identifier,
        int branchId,
        DateTime requestCheckInDateUtc) // UTC
    {
        // 1) Resolve user & branch
        var endUser = await _context.EndUsers
            .FirstOrDefaultAsync(u => u.PhoneNumber == identifier || u.UniqueIdentifier == identifier);

        if (endUser is null) return (false, "User not found", null);

        var branch = await _context.Branches.FindAsync(branchId);

        if (branch is null) return (false, "Branch not found", null);
        if (!branch.IsActive) return (false, "Branch is not active", null);
        if (string.IsNullOrWhiteSpace(branch.TimeZoneId)) return (false, "Branch time zone not set", null);

        var tz = branch.TimeZoneId;

        // 2) Compute branch-local context
        var nowLocal = NodaTimeExtensions.GetLocalNow(tz);
        var todayLocalDate = nowLocal.Date;

        // Requested local date (once-per-local-day rule)
        var reqLocalDateTime = requestCheckInDateUtc.ToLocalTime(tz);
        var reqLocalDate = requestCheckInDateUtc.ToLocalTime(tz)
            .Date;

        // Subscription window (compare as branch-local dates)
        var subStartLocal = endUser.SubscriptionStartDate.ToLocalTime(tz)
            .Date;
        var subEndLocal = endUser.SubscriptionEndDate.ToLocalTime(tz)
            .Date;

        if (endUser.IsStopped)
            return (false, "Subscription is currently stopped by admin", null);

        // Pause window (as branch-local dates)
        if (endUser.IsPaused && endUser.CurrentPauseStartDate.HasValue)
        {
            var pauseStartLocal = endUser.CurrentPauseStartDate.Value.ToLocalTime(tz)
                .Date;
            var pauseEndLocal = endUser.CurrentPauseEndDate!.Value.ToLocalTime(tz)
                .Date;

            var isPausedOnRequested = reqLocalDate >= pauseStartLocal && reqLocalDate <= pauseEndLocal;

            if (isPausedOnRequested)
                return (false, $"Subscription was paused on {reqLocalDate:yyyy-MM-dd}", null);

            // Auto-unpause if pause ended and we're checking "today" locally
            if (reqLocalDate == todayLocalDate && todayLocalDate > pauseEndLocal)
                await UnpauseSubscriptionAsync(endUser.Id);
        }

        if (reqLocalDate < subStartLocal || reqLocalDate > subEndLocal)
            return (false,
                $"Subscription is not active for {reqLocalDate:yyyy-MM-dd}. Valid: {subStartLocal:yyyy-MM-dd} to {subEndLocal:yyyy-MM-dd}",
                null);

        // 3) Once-per-local-day: build UTC range for that local date and query in UTC
        var hasCheckedIn = await HasCheckedInOnDateAsync(endUser.Id, reqLocalDate, tz);

        if (hasCheckedIn)
        {
            var label = reqLocalDate == todayLocalDate ? "today" : reqLocalDate.ToString("yyyy-MM-dd");
            return (false, $"User has already checked in {label}", null);
        }

        // 4) Slot validation based on current branch-local time
        var isWithin = await IsWithinAllowedTimeSlotAsync(
            branchId,
            reqLocalDateTime.DayOfWeek,
            reqLocalDateTime.TimeOfDay);

        if (!isWithin)
        {
            var allowed = await GetBranchTimeSlotDisplayAsync(branchId, nowLocal.DayOfWeek);
            return (false, $"Check-in is only allowed during: {allowed}", null);
        }

        return (true, "Validation successful", endUser);
    }


    public async Task<(bool Success, string Message)> EditCheckInAsync(
        EditCheckInRequest request)
    {
        if (request is null || request.CheckInId <= 0)
            return new(false, "Invalid check-in data.");

        var checkIn = await _context.CheckIns
            .Include(c => c.EndUser)
            .Include(c => c.Branch) // need TimeZoneId
            .FirstOrDefaultAsync(c => c.Id == request.CheckInId);

        if (checkIn is null)
            return new(false, "Check-in record not found.");

        if (checkIn.Branch is null || string.IsNullOrWhiteSpace(checkIn.Branch.TimeZoneId))
            return new(false, "Branch time zone not set for this check-in.");

        var tz = checkIn.Branch.TimeZoneId;

        // Normalize incoming UTC datetime(s)
        var requestedUtc = request.CheckInDate.EnsureUtc();

        // Compute the branch-local date from requested UTC, then build the UTC day window
        var requestedLocalDate = requestedUtc.ToLocalTime(tz)
            .Date;
        var startUtc = requestedLocalDate.GetStartOfDayUtc(tz);
        var endUtc = requestedLocalDate.GetEndOfDayUtc(tz);

        // GLOBAL per-local-day: block if ANY other check-in (any branch) exists in this local day
        var duplicateExists = await _context.CheckIns.AnyAsync(x =>
            x.Id != checkIn.Id &&
            x.EndUserId == checkIn.EndUserId &&
            x.CheckInDateTime >= startUtc && x.CheckInDateTime <= endUtc);

        if (duplicateExists)
            return new(false, "There is a check-in on this local date.");

        // Update details
        checkIn.BranchCourtId = request.BranchCourtId;
        checkIn.PlayDuration =
            request.PlayDurationMinutes > 0 ? TimeSpan.FromMinutes(request.PlayDurationMinutes) : null;
        checkIn.PlayStartTime = request.PlayStartTime.HasValue ? request.PlayStartTime.Value.EnsureUtc() : null;
        checkIn.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        checkIn.CheckInDateTime = requestedUtc; // store as UTC

        // Attendance & warnings
        checkIn.PlayerAttended = request.PlayerAttended;
        var (isAutoStopped, warningMessage) =
            await _warningService.ProcessPlayerAttendanceAsync(request.CheckInId, request.PlayerAttended);

        await _context.SaveChangesAsync();

        var baseMsg = $"Check-in details updated successfully for {checkIn.EndUser.Name}";
        if (isAutoStopped || !request.PlayerAttended)
            return new(true, $"{baseMsg}. {warningMessage}");

        return new(true, baseMsg);
    }

    public async Task<(bool Success, string Message, int? CheckInId)> AdminManualCheckInAsync(
        string phoneNumber,
        int branchId,
        DateTime checkInDateTime,
        int branchCourtId,
        int? playDurationMinutes = null,
        DateTime? playStartTime = null,
        string notes = null,
        bool playerAttended = true)
    {
        try
        {
            // Find user by phone number
            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

            if (endUser == null)
            {
                return (false, "User not found with the provided phone number", null);
            }

            // Validate branch exists
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null)
            {
                return (false, "Selected branch not found", null);
            }

            if (!branch.IsActive)
            {
                return (false, "Selected branch is not active", null);
            }

            // Convert KSA check-in date to UTC for database storage
            var checkInDateTimeUtc = checkInDateTime;

            // Check if user already has a check-in on this date
            var checkInDateKSA = checkInDateTime.ToKSATime()
                .Date;
            var existingCheckIns = await _context.CheckIns
                .Where(c => c.EndUserId == endUser.Id)
                .ToListAsync();

            var hasExistingCheckIn = existingCheckIns.Any(c =>
                c.CheckInDateTime.ToKSATime()
                    .Date == checkInDateKSA);

            if (hasExistingCheckIn)
            {
                return (false, $"User already has a check-in record for {checkInDateKSA:MMM dd, yyyy}", null);
            }


            var nowKSA = KSADateTimeExtensions.GetKSANow();
            var todayKSA = nowKSA.Date;
            if (endUser.IsPaused && endUser.CurrentPauseStartDate!.Value.ToKSATime()
                    .Date <= todayKSA.Date)
            {
                var pauseEndDateKSA = endUser.CurrentPauseEndDate?.ToKSATime()
                    .Date;
                if (pauseEndDateKSA.HasValue && todayKSA > pauseEndDateKSA.Value)
                {
                    await UnpauseSubscriptionAsync(endUser.Id);
                }
            }

            // Create check-in record
            var checkIn = new CheckIn
            {
                EndUserId = endUser.Id,
                BranchId = branchId,
                CheckInDateTime = checkInDateTimeUtc,
                BranchCourtId = branchCourtId,
                PlayDuration =
                    playDurationMinutes.HasValue ? TimeSpan.FromMinutes(playDurationMinutes.Value) : null,
                PlayStartTime = playStartTime,
                Notes = !string.IsNullOrWhiteSpace(notes) ? notes.Trim() : null,
                PlayerAttended = playerAttended
            };

            _context.CheckIns.Add(checkIn);
            await _context.SaveChangesAsync();

            var baseMessage = $"Manual check-in created successfully for {endUser.Name}";
            var (isAutoStopped, warningMessage) =
                await _warningService.ProcessPlayerAttendanceAsync(checkIn.Id, playerAttended);

            if (isAutoStopped || !playerAttended)
            {
                return (true, $"{baseMessage}. {warningMessage}", checkIn.Id);
            }

            return (true, baseMessage, checkIn.Id);
        }
        catch (Exception ex)
        {
            // Log the exception here
            return (false, "An error occurred while creating manual check-in", null);
        }
    }

    public async Task<(bool IsValid, string Message, EndUser User)> ValidateEndUserForManualCheckInAsync(
        ValidateUserRequest request)
    {
        try
        {
            // 1) Find user
            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

            if (endUser is null)
                return (false, "User not found with the provided phone number", null);

            // 2) pass the branch TZ here.
            var branch = await _context.Branches.FindAsync(request.BranchId);

            if (branch is null)
                return (false, "Branch not found", null);

            if (!branch.IsActive)
                return (false, "Branch is not active", null);

            var tz = branch.TimeZoneId;

            // Requested local date (assumes request.CheckInDate is UTC or should be treated as UTC if unspecified)
            var reqLocalDate = request.CheckInDate.ToLocalTime(tz)
                .Date;

            // Subscription window (local KSA dates)
            var subStartLocal = endUser.SubscriptionStartDate.ToLocalTime(tz)
                .Date;
            var subEndLocal = endUser.SubscriptionEndDate.ToLocalTime(tz)
                .Date;

            if (reqLocalDate < subStartLocal || reqLocalDate > subEndLocal)
            {
                var startStr = subStartLocal.ToString("MMM dd, yyyy");
                var endStr = subEndLocal.ToString("MMM dd, yyyy");
                return (false, $"Subscription is not valid for selected date. Valid period: {startStr} to {endStr}",
                    null);
            }

            // Pause window (local)
            if (endUser.IsPaused)
            {
                var pauseStartLocal = endUser.CurrentPauseStartDate!.Value.ToLocalTime(tz)
                    .Date;
                var pauseEndLocal = endUser.CurrentPauseEndDate!.Value.ToLocalTime(tz)
                    .Date;

                if (reqLocalDate >= pauseStartLocal && reqLocalDate <= pauseEndLocal)
                {
                    return (false,
                        $"Subscription was paused on the selected date ({pauseStartLocal:MMM dd, yyyy} to {pauseEndLocal:MMM dd, yyyy})",
                        null);
                }
            }

            // 3) Once-per-local-day (KSA): build UTC range for that local date and query in UTC
            var hasExistingCheckIn = await HasCheckedInOnDateAsync(endUser.Id, reqLocalDate, tz);


            if (hasExistingCheckIn)
                return (false, $"User already has a check-in record for {reqLocalDate:MMM dd, yyyy}", null);

            return (true, "User is valid for manual check-in", endUser);
        }
        catch
        {
            return (false, "An error occurred while validating user", null);
        }
    }

    private async Task<bool> HasCheckedInOnDateAsync(
        int endUserId,
        DateTime checkInLocalDate, // Local date
        string branchTimeZoneId) // IANA time zone
    {
        // Build UTC range for the given local date in the branch's time zone
        var startUtc = checkInLocalDate.GetStartOfDayUtc(branchTimeZoneId);
        var endUtc = checkInLocalDate.GetEndOfDayUtc(branchTimeZoneId);

        // Query directly in UTC for efficiency
        return await _context.CheckIns
            .AnyAsync(c =>
                c.EndUserId == endUserId &&
                c.CheckInDateTime >= startUtc &&
                c.CheckInDateTime <= endUtc);
    }
}