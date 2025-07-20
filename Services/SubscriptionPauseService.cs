using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Models.ViewModels.PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Services
{
    public interface ISubscriptionPauseService
    {
        Task<(bool Success, string Message)> PauseSubscriptionAsync(int endUserId, DateTime pauseStartDate, int pauseDays, string reason, string createdByUserId);
        Task<(bool Success, string Message)> UnpauseSubscriptionAsync(int endUserId, string createdByUserId);
        Task<List<SubscriptionPauseHistoryViewModel>> GetPauseHistoryAsync(int endUserId);
        Task<List<SubscriptionPauseHistoryViewModel>> GetAllPauseHistoryAsync();
        Task<bool> IsSubscriptionCurrentlyPausedAsync(int endUserId);
        Task<DateTime> GetEffectiveSubscriptionEndDateAsync(int endUserId);
    }

    public class SubscriptionPauseService : ISubscriptionPauseService
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionPauseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message)> PauseSubscriptionAsync(int endUserId, DateTime pauseStartDate, int pauseDays, string reason, string createdByUserId)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null)
            {
                return (false, "End user not found");
            }

            if (endUser.IsPaused)
            {
                return (false, "Subscription is already paused");
            }

            // Validate pause start date
            if (pauseStartDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Pause start date cannot be in the past");
            }

            // Check if pause start date is within subscription period
            if (pauseStartDate.Date > endUser.SubscriptionEndDate.Date)
            {
                return (false, "Pause start date is after subscription end date");
            }

            var pauseEndDate = pauseStartDate.AddDays(pauseDays - 1); // -1 because we include the start day

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create pause record
                var subscriptionPause = new SubscriptionPause
                {
                    EndUserId = endUserId,
                    PauseStartDate = pauseStartDate,
                    PauseDays = pauseDays,
                    PauseEndDate = pauseEndDate,
                    Reason = reason,
                    CreatedByUserId = createdByUserId,
                    IsActive = true
                };

                _context.SubscriptionPauses.Add(subscriptionPause);

                // Update end user pause status
                endUser.IsPaused = true;
                endUser.CurrentPauseStartDate = pauseStartDate;
                endUser.CurrentPauseEndDate = pauseEndDate;

                // Extend subscription end date by pause days
                endUser.SubscriptionEndDate = endUser.SubscriptionEndDate.AddDays(pauseDays);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Subscription paused for {pauseDays} days from {pauseStartDate:MMM dd, yyyy} to {pauseEndDate:MMM dd, yyyy}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error pausing subscription: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UnpauseSubscriptionAsync(int endUserId, string createdByUserId)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null)
            {
                return (false, "End user not found");
            }

            if (!endUser.IsPaused)
            {
                return (false, "Subscription is not currently paused");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Mark current active pause as completed
                var activePause = await _context.SubscriptionPauses
                    .FirstOrDefaultAsync(sp => sp.EndUserId == endUserId && sp.IsActive);

                if (activePause != null)
                {
                    activePause.IsActive = false;
                    
                    // Calculate actual pause days used
                    var today = DateTime.UtcNow.Date;
                    var actualPauseDays = 0;
                    
                    if (today >= activePause.PauseStartDate.Date && today <= activePause.PauseEndDate.Date)
                    {
                        actualPauseDays = (today - activePause.PauseStartDate.Date).Days + 1;
                    }
                    else if (today > activePause.PauseEndDate.Date)
                    {
                        actualPauseDays = activePause.PauseDays;
                    }

                    // Adjust subscription end date based on actual pause days used
                    var unusedPauseDays = activePause.PauseDays - actualPauseDays;
                    if (unusedPauseDays > 0)
                    {
                        endUser.SubscriptionEndDate = endUser.SubscriptionEndDate.AddDays(-unusedPauseDays);
                    }
                }

                // Update end user pause status
                endUser.IsPaused = false;
                endUser.CurrentPauseStartDate = null;
                endUser.CurrentPauseEndDate = null;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Subscription unpaused successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error unpausing subscription: {ex.Message}");
            }
        }

        public async Task<List<SubscriptionPauseHistoryViewModel>> GetPauseHistoryAsync(int endUserId)
        {
            return await _context.SubscriptionPauses
                .Include(sp => sp.CreatedByUser)
                .Include(sp => sp.EndUser)
                .Where(sp => sp.EndUserId == endUserId)
                .OrderByDescending(sp => sp.CreatedAt)
                .Select(sp => new SubscriptionPauseHistoryViewModel
                {
                    Id = sp.Id,
                    EndUserName = sp.EndUser.Name,
                    PauseStartDate = sp.PauseStartDate,
                    PauseEndDate = sp.PauseEndDate,
                    PauseDays = sp.PauseDays,
                    Reason = sp.Reason,
                    CreatedByUserName = sp.CreatedByUser.FullName,
                    CreatedAt = sp.CreatedAt,
                    IsActive = sp.IsActive
                })
                .ToListAsync();
        }

        public async Task<List<SubscriptionPauseHistoryViewModel>> GetAllPauseHistoryAsync()
        {
            return await _context.SubscriptionPauses
                .Include(sp => sp.CreatedByUser)
                .Include(sp => sp.EndUser)
                .OrderByDescending(sp => sp.CreatedAt)
                .Select(sp => new SubscriptionPauseHistoryViewModel
                {
                    Id = sp.Id,
                    EndUserName = sp.EndUser.Name,
                    PauseStartDate = sp.PauseStartDate,
                    PauseEndDate = sp.PauseEndDate,
                    PauseDays = sp.PauseDays,
                    Reason = sp.Reason,
                    CreatedByUserName = sp.CreatedByUser.FullName,
                    CreatedAt = sp.CreatedAt,
                    IsActive = sp.IsActive
                })
                .ToListAsync();
        }

        public async Task<bool> IsSubscriptionCurrentlyPausedAsync(int endUserId)
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null || !endUser.IsPaused)
            {
                return false;
            }

            var today = DateTime.UtcNow.Date;
            return endUser.CurrentPauseStartDate?.Date <= today && 
                   endUser.CurrentPauseEndDate?.Date >= today;
        }

        public async Task<DateTime> GetEffectiveSubscriptionEndDateAsync(int endUserId)
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
    }
}