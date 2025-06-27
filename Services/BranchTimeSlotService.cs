using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Services
{
    public interface IBranchTimeSlotService
    {
        Task<(bool Success, string Message)> AddTimeSlotAsync(int branchId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime);
        Task<(bool Success, string Message)> UpdateTimeSlotAsync(int timeSlotId, TimeSpan startTime, TimeSpan endTime, bool isActive);
        Task<(bool Success, string Message)> DeleteTimeSlotAsync(int timeSlotId);
        Task<List<BranchTimeSlotViewModel>> GetBranchTimeSlotsAsync(int branchId);
        Task<List<BranchTimeSlotViewModel>> GetAllTimeSlotsAsync();
        Task<bool> IsTimeSlotValidAsync(TimeSpan startTime, TimeSpan endTime);
        Task<bool> HasConflictingTimeSlotAsync(int branchId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime, int? excludeTimeSlotId = null);
    }

    public class BranchTimeSlotService : IBranchTimeSlotService
    {
        private readonly ApplicationDbContext _context;

        public BranchTimeSlotService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message)> AddTimeSlotAsync(int branchId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime)
        {
            // Validate branch exists
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null)
            {
                return (false, "Branch not found");
            }

            // Validate time slot
            if (!IsTimeSlotValidAsync(startTime, endTime).Result)
            {
                return (false, "Invalid time slot. End time must be at least 30 minutes after start time");
            }

            // Check for conflicts
            if (await HasConflictingTimeSlotAsync(branchId, dayOfWeek, startTime, endTime))
            {
                return (false, "This time slot conflicts with an existing time slot for the same day");
            }

            var timeSlot = new BranchTimeSlot
            {
                BranchId = branchId,
                DayOfWeek = dayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                IsActive = true
            };

            _context.BranchTimeSlots.Add(timeSlot);
            await _context.SaveChangesAsync();

            return (true, $"Time slot added successfully for {dayOfWeek}: {startTime:hh\\:mm} - {endTime:hh\\:mm}");
        }

        public async Task<(bool Success, string Message)> UpdateTimeSlotAsync(int timeSlotId, TimeSpan startTime, TimeSpan endTime, bool isActive)
        {
            var timeSlot = await _context.BranchTimeSlots.FindAsync(timeSlotId);
            if (timeSlot == null)
            {
                return (false, "Time slot not found");
            }

            // Validate time slot
            if (!await IsTimeSlotValidAsync(startTime, endTime))
            {
                return (false, "Invalid time slot. End time must be at least 30 minutes after start time");
            }

            // Check for conflicts (excluding current time slot)
            if (await HasConflictingTimeSlotAsync(timeSlot.BranchId, timeSlot.DayOfWeek, startTime, endTime, timeSlotId))
            {
                return (false, "This time slot conflicts with an existing time slot for the same day");
            }

            timeSlot.StartTime = startTime;
            timeSlot.EndTime = endTime;
            timeSlot.IsActive = isActive;

            await _context.SaveChangesAsync();

            return (true, "Time slot updated successfully");
        }

        public async Task<(bool Success, string Message)> DeleteTimeSlotAsync(int timeSlotId)
        {
            var timeSlot = await _context.BranchTimeSlots.FindAsync(timeSlotId);
            if (timeSlot == null)
            {
                return (false, "Time slot not found");
            }

            _context.BranchTimeSlots.Remove(timeSlot);
            await _context.SaveChangesAsync();

            return (true, "Time slot deleted successfully");
        }

        public async Task<List<BranchTimeSlotViewModel>> GetBranchTimeSlotsAsync(int branchId)
        {
            return await _context.BranchTimeSlots
                .Include(ts => ts.Branch)
                .Where(ts => ts.BranchId == branchId)
                .OrderBy(ts => ts.DayOfWeek)
                .ThenBy(ts => ts.StartTime)
                .Select(ts => new BranchTimeSlotViewModel
                {
                    Id = ts.Id,
                    BranchId = ts.BranchId,
                    BranchName = ts.Branch.Name,
                    DayOfWeek = ts.DayOfWeek,
                    StartTime = ts.StartTime.ToString(@"hh\:mm"),
                    EndTime = ts.EndTime.ToString(@"hh\:mm"),
                    TimeRange = $"{ts.StartTime:hh\\:mm} - {ts.EndTime:hh\\:mm}",
                    IsActive = ts.IsActive
                })
                .ToListAsync();
        }

        public async Task<List<BranchTimeSlotViewModel>> GetAllTimeSlotsAsync()
        {
            return await _context.BranchTimeSlots
                .Include(ts => ts.Branch)
                .OrderBy(ts => ts.Branch.Name)
                .ThenBy(ts => ts.DayOfWeek)
                .ThenBy(ts => ts.StartTime)
                .Select(ts => new BranchTimeSlotViewModel
                {
                    Id = ts.Id,
                    BranchId = ts.BranchId,
                    BranchName = ts.Branch.Name,
                    DayOfWeek = ts.DayOfWeek,
                    StartTime = ts.StartTime.ToString(@"hh\:mm"),
                    EndTime = ts.EndTime.ToString(@"hh\:mm"),
                    TimeRange = $"{ts.StartTime:hh\\:mm} - {ts.EndTime:hh\\:mm}",
                    IsActive = ts.IsActive
                })
                .ToListAsync();
        }

        public async Task<bool> IsTimeSlotValidAsync(TimeSpan startTime, TimeSpan endTime)
        {
            // For time slots that cross midnight (e.g., 22:00 to 04:00)
            if (startTime > endTime)
            {
                // Calculate duration by adding 24 hours to end time
                var duration = TimeSpan.FromHours(24) - startTime + endTime;
                return duration >= TimeSpan.FromMinutes(30);
            }
            else
            {
                // Normal time slot
                return endTime - startTime >= TimeSpan.FromMinutes(30);
            }
        }

        public async Task<bool> HasConflictingTimeSlotAsync(int branchId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime, int? excludeTimeSlotId = null)
        {
            var existingSlots = await _context.BranchTimeSlots
                .Where(ts => ts.BranchId == branchId && 
                           ts.DayOfWeek == dayOfWeek && 
                           ts.IsActive)
                .Where(ts => excludeTimeSlotId == null || ts.Id != excludeTimeSlotId)
                .ToListAsync();

            foreach (var existingSlot in existingSlots)
            {
                if (TimeSlotsOverlap(startTime, endTime, existingSlot.StartTime, existingSlot.EndTime))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TimeSlotsOverlap(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2)
        {
            // Handle time slots that cross midnight
            bool slot1CrossesMidnight = start1 > end1;
            bool slot2CrossesMidnight = start2 > end2;

            if (!slot1CrossesMidnight && !slot2CrossesMidnight)
            {
                // Both slots are within the same day
                return start1 < end2 && start2 < end1;
            }
            else if (slot1CrossesMidnight && !slot2CrossesMidnight)
            {
                // Slot1 crosses midnight, slot2 doesn't
                return (start1 <= end2) || (start2 <= end1);
            }
            else if (!slot1CrossesMidnight && slot2CrossesMidnight)
            {
                // Slot2 crosses midnight, slot1 doesn't
                return (start2 <= end1) || (start1 <= end2);
            }
            else
            {
                // Both slots cross midnight - they always overlap
                return true;
            }
        }
    }
}