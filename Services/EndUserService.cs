using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Services;

public class EndUserService : IEndUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EndUserService> _logger;

    public EndUserService(ApplicationDbContext context, ILogger<EndUserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EndUsersPaginatedViewModel> GetEndUsersAsync(string searchPhoneNumber, int page = 1, int pageSize = 10)
    {
        // Build base filtered query (without pagination)
        var baseQuery = _context.EndUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchPhoneNumber))
        {
            searchPhoneNumber = searchPhoneNumber.Trim();
            baseQuery = baseQuery.Where(e => e.PhoneNumber.Contains(searchPhoneNumber));
        }

        // Compute statistics via SQL (before pagination)
        var todayKsaDate = KSADateTimeExtensions.GetKSANow().Date;
        var startOfKsaDayUtc = todayKsaDate.GetStartOfKSADayInUTC();
        var endOfKsaDayUtc = todayKsaDate.GetEndOfKSADayInUTC();

        var activeSubscriptions = await baseQuery
            .CountAsync(u => u.SubscriptionStartDate <= endOfKsaDayUtc
                             && u.SubscriptionEndDate >= startOfKsaDayUtc
                             && !(
                                 u.IsPaused &&
                                 u.CurrentPauseStartDate != null &&
                                 u.CurrentPauseEndDate != null &&
                                 u.CurrentPauseStartDate <= endOfKsaDayUtc &&
                                 u.CurrentPauseEndDate >= startOfKsaDayUtc
                             )
                             && !u.IsStopped);

        var currentlyPaused = await baseQuery
            .CountAsync(u => u.IsPaused
                             && !u.IsStopped
                             && u.CurrentPauseStartDate != null
                             && u.CurrentPauseEndDate != null
                             && u.CurrentPauseStartDate <= endOfKsaDayUtc
                             && u.CurrentPauseEndDate >= startOfKsaDayUtc);

        var stoppedCount = await baseQuery.CountAsync(u => u.IsStopped);

        var expiredCount = await baseQuery
            .CountAsync(u => !u.IsStopped && u.SubscriptionEndDate < startOfKsaDayUtc);

        var notSetPlaytomicUserIdsCount = await baseQuery.CountAsync(u => u.PlaytomicUserId == null);

        var stoppedByWarningsCount = await baseQuery.CountAsync(u => u.IsStoppedByWarning);

        // Pagination remains in DB
        var orderedQuery = baseQuery.OrderByDescending(e => e.CreatedAt);

        var totalItems = await orderedQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        var endUsers = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new EndUsersPaginatedViewModel
        {
            EndUsers = new PaginatedResult<EndUser>
            {
                Items = endUsers,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize
            },
            SearchPhoneNumber = searchPhoneNumber,
            ActiveSubscriptions = activeSubscriptions,
            CurrentlyPaused = currentlyPaused,
            StoppedCount = stoppedCount,
            ExpiredCount = expiredCount,
            NotSetPlaytomicUserIdsCount = notSetPlaytomicUserIdsCount,
            StoppedByWarningsCount = stoppedByWarningsCount
        };
    }

    public async Task<(bool Success, string Message, EndUser? EndUser)> CreateEndUserAsync(EndUserViewModel model)
    {
        try
        {
            // Check if the end user already exists by phone number or email
            var existingUser = await _context.EndUsers
                .FirstOrDefaultAsync(e => e.PhoneNumber == model.PhoneNumber || e.Email == model.Email.ToLower());

            if (existingUser != null)
            {
                return (false, "An end user with the same phone number or email already exists.", null);
            }

            // Convert KSA dates to UTC for storage
            var subscriptionStartUtc = model.SubscriptionStartDate;
            var subscriptionEndUtc = model.SubscriptionEndDate;

            var endUser = new EndUser
            {
                Name = model.Name,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email.ToLower(),
                ImageUrl = model.ImageUrl,
                SubscriptionStartDate = subscriptionStartUtc,
                SubscriptionEndDate = subscriptionEndUtc,
                UniqueIdentifier = Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 8)
                    .ToUpper()
            };

            _context.EndUsers.Add(endUser);
            await _context.SaveChangesAsync();

            return (true, "End user created successfully!", endUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating end user with phone number: {PhoneNumber}", model.PhoneNumber);
            return (false, "An error occurred while creating the end user.", null);
        }
    }

    public async Task<(bool Success, string Message)> UpdateEndUserAsync(int id, EndUserViewModel model)
    {
        try
        {
            var endUser = await _context.EndUsers.FindAsync(id);

            if (endUser == null)
            {
                return (false, "End user not found.");
            }

            if ((endUser.PhoneNumber != model.PhoneNumber &&
                 await _context.EndUsers.AnyAsync(e => e.PhoneNumber == model.PhoneNumber)) ||
                (endUser.Email != model.Email.ToLower() &&
                 await _context.EndUsers.AnyAsync(e => e.Email == model.Email.ToLower())))
            {
                return (false, "An end user with the same phone number or email already exists.");
            }

            // Convert KSA dates to UTC for storage
            var subscriptionStartUtc = model.SubscriptionStartDate;
            var subscriptionEndUtc = model.SubscriptionEndDate;

            endUser.Name = model.Name;
            endUser.PhoneNumber = model.PhoneNumber;
            endUser.Email = model.Email.ToLower();
            endUser.ImageUrl = model.ImageUrl;
            endUser.SubscriptionStartDate = subscriptionStartUtc;
            endUser.SubscriptionEndDate = subscriptionEndUtc;

            await _context.SaveChangesAsync();
            return (true, "End user updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating end user with ID: {EndUserId}", id);
            return (false, "An error occurred while updating the end user.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteEndUserAsync(int id)
    {
        try
        {
            var endUser = await _context.EndUsers.FindAsync(id);

            if (endUser == null)
            {
                return (false, "End user not found.");
            }

            _context.EndUsers.Remove(endUser);
            await _context.SaveChangesAsync();
            return (true, "End user deleted successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting end user with ID: {EndUserId}", id);
            return (false, "An error occurred while deleting the end user.");
        }
    }

    public async Task<EndUser?> GetEndUserByIdAsync(int id)
    {
        return await _context.EndUsers.FindAsync(id);
    }

    public async Task<(bool Success, string Message, string? DownloadUrl)> GenerateQRCodeAsync(int endUserId, bool forceRegenerate = false, string baseUrl = "")
    {
        try
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null)
            {
                return (false, "End user not found.", null);
            }

            // Check if QR has already been downloaded and not forcing regeneration
            if (endUser.HasDownloadedQR && !forceRegenerate)
            {
                return (false, "QR code has already been downloaded.", null);
            }

            // Generate a new token and reset the download status
            endUser.QRCodeDownloadToken = Guid.NewGuid().ToString("N");
            endUser.HasDownloadedQR = false;
            await _context.SaveChangesAsync();

            // Generate the download URL
            var downloadUrl = $"{baseUrl}/QRCode/Download?token={endUser.QRCodeDownloadToken}";

            var message = forceRegenerate
                ? "New QR code generated successfully."
                : "QR code download link generated successfully.";

            return (true, message, downloadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QR code for end user ID: {EndUserId}", endUserId);
            return (false, "An error occurred while generating the QR code.", null);
        }
    }

    public async Task<(bool Success, string Message)> StopSubscriptionAsync(int endUserId, string stopReason)
    {
        try
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);

            if (endUser == null)
            {
                return (false, "End user not found.");
            }

            if (endUser.IsStopped)
            {
                return (false, "Subscription is already stopped.");
            }

            // Stop the subscription
            endUser.IsStopped = true;
            endUser.StoppedDate = DateTime.UtcNow;
            endUser.StopReason = stopReason;

            // If the subscription was paused, unpause it when stopping
            if (endUser.IsPaused)
            {
                endUser.IsPaused = false;
                endUser.CurrentPauseStartDate = null;
                endUser.CurrentPauseEndDate = null;
            }

            await _context.SaveChangesAsync();
            return (true, $"Subscription for {endUser.Name} has been stopped successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping subscription for EndUser ID: {EndUserId}", endUserId);
            return (false, "An error occurred while stopping the subscription.");
        }
    }

    public async Task<(bool Success, string Message)> ReactivateSubscriptionAsync(int endUserId)
    {
        try
        {
            var endUser = await _context.EndUsers.FindAsync(endUserId);
            if (endUser == null)
            {
                return (false, "End user not found.");
            }

            if (!endUser.IsStopped)
            {
                return (false, "Subscription is not stopped.");
            }

            // Reactivate the subscription
            endUser.IsStopped = false;
            endUser.StoppedDate = null;
            endUser.StopReason = null;

            if (endUser.IsStoppedByWarning)
            {
                endUser.IsStoppedByWarning = false;
                endUser.WarningCount = 0;
            }

            await _context.SaveChangesAsync();
            return (true, $"Subscription for {endUser.Name} has been reactivated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating subscription for EndUser ID: {EndUserId}", endUserId);
            return (false, "An error occurred while reactivating the subscription.");
        }
    }
}
