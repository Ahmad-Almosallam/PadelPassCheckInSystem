using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;

namespace PadelPassCheckInSystem.Services
{
    public class CheckInService : ICheckInService
    {
        private readonly ApplicationDbContext _context;

        public CheckInService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message, int? CheckInId)> CheckInAsync(string identifier, int branchId)
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

        public async Task<(bool Success, string Message)> AssignCourtAsync(int checkInId, string courtName, int playDurationMinutes, DateTime? playStartTime, string notes)
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

            // Update check-in with court assignment
            checkIn.CourtName = courtName;
            checkIn.PlayDuration = TimeSpan.FromMinutes(playDurationMinutes);
            checkIn.PlayStartTime = playStartTime ?? DateTime.UtcNow;
            checkIn.Notes = notes;

            await _context.SaveChangesAsync();

            return (true, $"Court '{courtName}' assigned successfully to {checkIn.EndUser.Name}");
        }

        public async Task<(bool Success, string Message)> DeleteCheckInAsync(int checkInId, int branchId)
        {
            var checkIn = await _context.CheckIns
                .Include(c => c.EndUser)
                .Include(c => c.Branch)
                .FirstOrDefaultAsync(c => c.Id == checkInId);

            if (checkIn == null)
            {
                return (false, "Check-in record not found");
            }

            // Verify that the check-in belongs to the specified branch
            if (checkIn.BranchId != branchId)
            {
                return (false, "You can only delete check-ins from your branch");
            }

            // Only allow deletion of today's check-ins
            var today = DateTime.UtcNow.Date;
            if (checkIn.CheckInDateTime.Date != today)
            {
                return (false, "You can only delete today's check-ins");
            }

            var userName = checkIn.EndUser.Name;
            
            _context.CheckIns.Remove(checkIn);
            await _context.SaveChangesAsync();

            return (true, $"Check-in for {userName} has been deleted successfully");
        }

        public async Task<bool> HasCheckedInTodayAsync(int endUserId)
        {
            var today = DateTime.UtcNow.Date;
            return await _context.CheckIns
                .AnyAsync(c => c.EndUserId == endUserId && c.CheckInDateTime.Date == today);
        }

        private async Task<bool> IsWithinAllowedTimeSlotAsync(int branchId, DayOfWeek dayOfWeek, TimeSpan currentTime)
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

        private async Task<string> GetBranchTimeSlotDisplayAsync(int branchId, DayOfWeek dayOfWeek)
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

        private async Task<DateTime> GetEffectiveSubscriptionEndDateAsync(int endUserId)
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

        private async Task UnpauseSubscriptionAsync(int endUserId)
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

        public async Task<List<CheckIn>> GetPendingCourtAssignmentsAsync(int branchId)
        {
            var today = DateTime.UtcNow.Date;
            return await _context.CheckIns
                .Include(c => c.EndUser)
                .Where(c => c.BranchId == branchId && 
                          c.CheckInDateTime.Date == today && 
                          string.IsNullOrEmpty(c.CourtName))
                .OrderBy(c => c.CheckInDateTime)
                .ToListAsync();
        }

        public async Task<List<CheckIn>> GetTodayCheckInsWithCourtInfoAsync(int branchId)
        {
            var today = DateTime.UtcNow.Date;
            return await _context.CheckIns
                .Include(c => c.EndUser)
                .Where(c => c.BranchId == branchId && c.CheckInDateTime.Date == today)
                .OrderByDescending(c => c.CheckInDateTime)
                .ToListAsync();
        }

        public async Task<(bool IsValid, string Message, EndUser User)> ValidateCheckInAsync(string identifier, int branchId)
        {
            // Find user by phone or unique identifier
            var endUser = await _context.EndUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == identifier || u.UniqueIdentifier == identifier);

            if (endUser == null)
            {
                return (false, "User not found", null);
            }

            var today = DateTime.UtcNow.Date;
            var currentTime = DateTime.UtcNow.TimeOfDay;
            var currentDayOfWeek = DateTime.UtcNow.DayOfWeek;

            // Check if subscription is paused
            if (endUser.IsPaused)
            {
                var pauseEndDate = endUser.CurrentPauseEndDate?.Date;
                if (pauseEndDate.HasValue && today <= pauseEndDate.Value)
                {
                    return (false, $"Subscription is currently paused until {pauseEndDate.Value:MMM dd, yyyy}", null);
                }
                else
                {
                    // Auto-unpause if pause period has ended
                    await UnpauseSubscriptionAsync(endUser.Id);
                }
            }

            // Check subscription validity
            if (today < endUser.SubscriptionStartDate.Date || today > endUser.SubscriptionEndDate.Date)
            {
                return (false, "Subscription is not active", null);
            }

            // Check if user has already checked in today
            var hasCheckedInToday = await HasCheckedInTodayAsync(endUser.Id);
            if (hasCheckedInToday)
            {
                return (false, "User has already checked in today", null);
            }

            // Check if current time is within allowed branch time slots
            var isWithinAllowedTime = await IsWithinAllowedTimeSlotAsync(branchId, currentDayOfWeek, currentTime);
            if (!isWithinAllowedTime)
            {
                var allowedTimes = await GetBranchTimeSlotDisplayAsync(branchId, currentDayOfWeek);
                return (false, $"Check-in is only allowed during: {allowedTimes}", null);
            }

            return (true, "Validation successful", endUser);
        }
    }
}