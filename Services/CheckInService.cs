using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Extensions;

namespace PadelPassCheckInSystem.Services
{
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
            int branchId)
        {
            var (isValid, message, endUser) = await ValidateCheckInAsync(identifier, branchId);

            if (!isValid)
            {
                return (false, message, null);
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

            return (true, $"Check-in successful for {endUser.Name}", checkIn.Id);
        }

        public async Task<(bool Success, string Message)> AssignCourtAsync(
            int checkInId,
            string courtName,
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
            checkIn.CourtName = courtName;
            checkIn.PlayDuration = TimeSpan.FromMinutes(playDurationMinutes);
            checkIn.PlayStartTime = playStartTimeUtc ?? DateTime.UtcNow;
            checkIn.Notes = notes.Trim().IsNullOrEmpty() ? null : notes.Trim();
            checkIn.PlayerAttended = playerAttended;

            await _context.SaveChangesAsync();

            var baseMessage = $"Court '{courtName}' assigned successfully to {checkIn.EndUser.Name}";

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

        public async Task<List<CheckIn>> GetTodayCheckInsWithCourtInfoAsync(
            int branchId)
        {
            // Use KSA date for filtering today's check-ins
            var todayKSA = KSADateTimeExtensions.GetKSANow()
                .Date;

            var allCheckIns = await _context.CheckIns
                .Include(c => c.EndUser)
                .Where(c => c.BranchId == branchId)
                .ToListAsync();

            // Filter by KSA date and order by check-in time
            return allCheckIns
                .Where(c => c.CheckInDateTime.ToKSATime()
                    .Date == todayKSA)
                .OrderByDescending(c => c.CheckInDateTime)
                .ToList();
        }

        public async Task<(bool IsValid, string Message, EndUser User)> ValidateCheckInAsync(
            string identifier,
            int branchId)
        {
            // Find user by phone or unique identifier
            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == identifier || u.UniqueIdentifier == identifier);

            if (endUser == null)
            {
                return (false, "User not found", null);
            }

            // Use KSA time for all validations
            var nowKSA = KSADateTimeExtensions.GetKSANow();
            var todayKSA = nowKSA.Date;
            var currentTimeKSA = nowKSA.TimeOfDay;
            var currentDayOfWeekKSA = nowKSA.DayOfWeek;

            // Convert subscription dates to KSA for comparison
            var subscriptionStartKSA = endUser.SubscriptionStartDate.ToKSATime()
                .Date;
            var subscriptionEndKSA = endUser.SubscriptionEndDate.ToKSATime()
                .Date;


            if (endUser.IsStopped)
            {
                return (false, $"Subscription is currently stopped by admin",
                    null);
            }

            // Check if subscription is paused (using KSA dates)
            if (endUser.IsPaused && endUser.CurrentPauseStartDate!.Value.ToKSATime()
                    .Date <= todayKSA.Date)
            {
                var pauseEndDateKSA = endUser.CurrentPauseEndDate?.ToKSATime()
                    .Date;
                if (pauseEndDateKSA.HasValue && todayKSA <= pauseEndDateKSA.Value)
                {
                    return (false, $"Subscription is currently paused until {pauseEndDateKSA.Value:MMM dd, yyyy}",
                        null);
                }
                else
                {
                    // Auto-unpause if pause period has ended
                    await UnpauseSubscriptionAsync(endUser.Id);
                }
            }

            // Check subscription validity using KSA dates
            if (todayKSA < subscriptionStartKSA || todayKSA > subscriptionEndKSA)
            {
                var startDateStr = subscriptionStartKSA.ToString("MMM dd, yyyy");
                var endDateStr = subscriptionEndKSA.ToString("MMM dd, yyyy");
                return (false, $"Subscription is not active. Valid period: {startDateStr} to {endDateStr}", null);
            }

            // Check if user has already checked in today (using KSA date)
            var hasCheckedInToday = await HasCheckedInTodayAsync(endUser.Id);
            if (hasCheckedInToday)
            {
                return (false, "User has already checked in today", null);
            }

            // Check if current time is within allowed branch time slots (using KSA time)
            var isWithinAllowedTime = await IsWithinAllowedTimeSlotAsync(branchId, currentDayOfWeekKSA, currentTimeKSA);
            if (!isWithinAllowedTime)
            {
                var allowedTimes = await GetBranchTimeSlotDisplayAsync(branchId, currentDayOfWeekKSA);
                return (false, $"Check-in is only allowed during: {allowedTimes}", null);
            }

            return (true, "Validation successful", endUser);
        }

        public async Task<(bool Success, string Message)> EditCheckInAsync(
            int checkInId,
            string courtName,
            int playDurationMinutes,
            DateTime? playStartTime,
            string notes)
        {
            var checkIn = await _context.CheckIns
                .Include(c => c.EndUser)
                .FirstOrDefaultAsync(c => c.Id == checkInId);

            if (checkIn == null)
            {
                return (false, "Check-in record not found");
            }

            // Convert KSA play start time to UTC for storage
            DateTime? playStartTimeUtc = null;
            if (playStartTime.HasValue)
            {
                // playStartTime is in KSA time, convert to UTC for storage
                playStartTimeUtc = playStartTime.Value;
            }

            // Update check-in details
            checkIn.CourtName = !string.IsNullOrWhiteSpace(courtName) ? courtName.Trim() : null;
            checkIn.PlayDuration = playDurationMinutes > 0 ? TimeSpan.FromMinutes(playDurationMinutes) : null;
            checkIn.PlayStartTime = playStartTimeUtc;
            checkIn.Notes = !string.IsNullOrWhiteSpace(notes) ? notes.Trim() : null;

            await _context.SaveChangesAsync();

            return (true, $"Check-in details updated successfully for {checkIn.EndUser.Name}");
        }

        public async Task<(bool Success, string Message, int? CheckInId)> AdminManualCheckInAsync(
            string phoneNumber,
            int branchId,
            DateTime checkInDateTime,
            string courtName = null,
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
                var checkInDateKSA = checkInDateTime.Date;
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

                // Create check-in record
                var checkIn = new CheckIn
                {
                    EndUserId = endUser.Id,
                    BranchId = branchId,
                    CheckInDateTime = checkInDateTimeUtc,
                    CourtName = !string.IsNullOrWhiteSpace(courtName) ? courtName.Trim() : null,
                    PlayDuration =
                        playDurationMinutes.HasValue ? TimeSpan.FromMinutes(playDurationMinutes.Value) : null,
                    PlayStartTime = playStartTime,
                    Notes = !string.IsNullOrWhiteSpace(notes) ? notes.Trim() : null,
                    PlayerAttended = playerAttended
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                var courtInfo = !string.IsNullOrEmpty(courtName) ? $" to {courtName}" : "";
                var baseMessage = $"Manual check-in created successfully for {endUser.Name}{courtInfo}";
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
            string phoneNumber,
            DateTime checkInDate)
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

                // Convert dates to KSA for validation
                var checkInDateKSA = checkInDate.Date;
                var subscriptionStartKSA = endUser.SubscriptionStartDate.ToKSATime()
                    .Date;
                var subscriptionEndKSA = endUser.SubscriptionEndDate.ToKSATime()
                    .Date;

                // Check subscription validity for the selected date
                if (checkInDateKSA < subscriptionStartKSA || checkInDateKSA > subscriptionEndKSA)
                {
                    var startDateStr = subscriptionStartKSA.ToString("MMM dd, yyyy");
                    var endDateStr = subscriptionEndKSA.ToString("MMM dd, yyyy");
                    return (false,
                        $"Subscription is not valid for selected date. Valid period: {startDateStr} to {endDateStr}",
                        null);
                }

                // Check if subscription was paused on the selected date
                if (endUser.IsPaused)
                {
                    var pauseStartKSA = endUser.CurrentPauseStartDate?.ToKSATime()
                        .Date;
                    var pauseEndKSA = endUser.CurrentPauseEndDate?.ToKSATime()
                        .Date;

                    if (pauseStartKSA.HasValue && pauseEndKSA.HasValue &&
                        checkInDateKSA >= pauseStartKSA.Value && checkInDateKSA <= pauseEndKSA.Value)
                    {
                        return (false,
                            $"Subscription was paused on the selected date ({pauseStartKSA.Value:MMM dd, yyyy} to {pauseEndKSA.Value:MMM dd, yyyy})",
                            null);
                    }
                }

                // Check if user already has a check-in on this date
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

                return (true, "User is valid for manual check-in", endUser);
            }
            catch (Exception ex)
            {
                // Log the exception here
                return (false, "An error occurred while validating user", null);
            }
        }
    }
}