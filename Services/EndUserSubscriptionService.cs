using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Integration.Rekaz.Enums;
using PadelPassCheckInSystem.Integration.Rekaz.Models;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;

namespace PadelPassCheckInSystem.Services;

public interface IEndUserSubscriptionService
{
    Task<bool> SyncRekazAsync(
        IGrouping<Guid, SubscriptionResponse> subs);

    Task<bool> ProcessWebhookEvent(
        WebhookEvent webhookEvent);
}

public class EndUserSubscriptionService(
    ApplicationDbContext context,
    IEndUserService endUserService,
    ILogger<EndUserSubscriptionService> logger) : IEndUserSubscriptionService
{
    public async Task<bool> SyncRekazAsync(
        IGrouping<Guid, SubscriptionResponse> subs)
    {
        try
        {
            var customerId = subs.Key;
            var subscriptions = subs.ToList();

            // 0) Guard: Validate input and find EndUser
            if (subscriptions.Count == 0 || customerId == Guid.Empty)
                return false;

            var endUser = await context.EndUsers
                .FirstOrDefaultAsync(e => e.RekazId == customerId);

            if (endUser is null)
                return false;

            // 1) Process all subscriptions for this customer
            var processedSubs = new List<ProcessedSubscription>();

            foreach (var sub in subscriptions)
            {
                var processedSub = ProcessSubscription(sub);
                if (processedSub != null)
                {
                    processedSubs.Add(processedSub);

                    // 2) Upsert subscription in database
                    await UpsertSubscription(processedSub, endUser.Id);
                }
            }

            if (!processedSubs.Any())
                return false;

            // 3) Determine the overall user subscription status using the processed subscriptions
            await UpdateEndUserSubscriptionStatus(endUser, processedSubs);

            return true;
        }
        catch (Exception ex)
        {
            // Log exception: ex.Message
            return false;
        }
    }


    #region SyncRekaz

    private ProcessedSubscription ProcessSubscription(
        SubscriptionResponse sub)
    {
        try
        {
            // Validate basic data
            if (sub?.Id == Guid.Empty || sub.StartAt == default || sub.EndAt == default)
                return null;

            // Normalize times as UTC
            var startUtc = NormalizeDate(sub.StartAt)
                .EnsureUtc();
            var endUtc = NormalizeDate(sub.EndAt)
                .EnsureUtc();
            var pauseAtUtc = sub.PausedAt?.EnsureUtc();
            var resumeAtUtc = sub.ResumeAt?.EnsureUtc();

            // Validate date ranges
            if (startUtc >= endUtc)
                return null;

            // Calculate pricing
            var price = sub.TotalAmount;
            var discount = sub.Discount?.Value ?? 0m;
            var isFullyPaid = sub.PaidAmount >= sub.TotalAmount || sub.TotalAmount == 0m;

            return new ProcessedSubscription
            {
                RekazId = sub.Id,
                StartDate = startUtc,
                EndDate = endUtc,
                Status = sub.Status,
                Name = sub.Items?.FirstOrDefault()
                    ?.Name ?? "Unknown Subscription",
                Price = price,
                Discount = discount,
                PaidAmount = sub.PaidAmount,
                IsFullyPaid = isFullyPaid,
                IsPaused = sub.IsPaused,
                PausedAt = pauseAtUtc,
                ResumedAt = resumeAtUtc,
                Code = sub.Code
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task UpsertSubscription(
        ProcessedSubscription processedSub,
        int endUserId)
    {
        var dbSub = await context.Set<EndUserSubscription>()
            .FirstOrDefaultAsync(x => x.RekazId == processedSub.RekazId);

        if (dbSub is null)
        {
            // Create new subscription record
            dbSub = new EndUserSubscription
            {
                RekazId = processedSub.RekazId,
                EndUserId = endUserId,
                StartDate = processedSub.StartDate,
                EndDate = processedSub.EndDate,
                Status = processedSub.Status,
                Name = processedSub.Name,
                Price = processedSub.Price,
                Discount = processedSub.Discount,
                IsPaused = processedSub.IsPaused,
                PausedAt = processedSub.PausedAt,
                ResumedAt = processedSub.ResumedAt,
                Code = processedSub.Code
            };

            context.Add(dbSub);
        }
        else
        {
            // Update existing subscription record
            dbSub.StartDate = processedSub.StartDate;
            dbSub.EndDate = processedSub.EndDate;
            dbSub.Status = processedSub.Status;
            dbSub.Name = processedSub.Name;
            dbSub.Price = processedSub.Price;
            dbSub.Discount = processedSub.Discount;
            dbSub.IsPaused = processedSub.IsPaused;
            dbSub.PausedAt = processedSub.PausedAt;
            dbSub.ResumedAt = processedSub.ResumedAt;
            dbSub.Code = processedSub.Code;

            context.Update(dbSub);
        }

        await context.SaveChangesAsync();
    }

    private async Task UpdateEndUserSubscriptionStatus(
        EndUser endUser,
        List<ProcessedSubscription> allSubs)
    {
        var nowUtc = DateTime.UtcNow;

        // Separate subscriptions by status for analysis
        var activeSubs = allSubs
            .Where(s => s.Status == SubscriptionStatus.Active && s.IsFullyPaid)
            .ToList();

        var pausedSubs = allSubs
            .Where(s => s.Status == SubscriptionStatus.Paused && s.IsFullyPaid)
            .ToList();

        var upcomingSubs = allSubs
            .Where(s => s.Status is SubscriptionStatus.Pending or SubscriptionStatus.StartingSoon
                        && s.StartDate > nowUtc && s.IsFullyPaid)
            .ToList();

        // Case 1: Find current active subscription
        var currentActiveSubscription = activeSubs
            .Where(s => IsWithinDateRange(s.StartDate, s.EndDate))
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.Price)
            .FirstOrDefault();

        // Case 2: Find current paused subscription
        var currentPausedSubscription = pausedSubs
            .Where(s => IsWithinDateRange(s.StartDate, s.EndDate))
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.Price)
            .FirstOrDefault();

        // Case 3: Find next upcoming subscription
        var nextUpcomingSubscription = upcomingSubs
            .OrderBy(s => s.StartDate)
            .ThenByDescending(s => s.Price)
            .FirstOrDefault();

        // Case 4: Handle overlapping subscriptions
        if (currentActiveSubscription != null && currentPausedSubscription != null)
        {
            // If both active and paused exist in same timeframe, prefer active
            // unless the paused one started later (upgrade scenario)
            if (currentPausedSubscription.StartDate > currentActiveSubscription.StartDate)
            {
                currentActiveSubscription = null; // Use paused one
            }
            else
            {
                currentPausedSubscription = null; // Use active one
            }
        }

        // Apply logic based on priority
        if (currentActiveSubscription != null)
        {
            await SetActiveSubscription(endUser, currentActiveSubscription);
        }
        else if (currentPausedSubscription != null)
        {
            await SetPausedSubscription(endUser, currentPausedSubscription);
        }
        else if (nextUpcomingSubscription != null)
        {
            await SetUpcomingSubscription(endUser, nextUpcomingSubscription);
        }
        else
        {
            await ClearActiveSubscription(endUser);
        }

        context.Update(endUser);
        await context.SaveChangesAsync();
        return;

        // Helper methods
        bool IsWithinDateRange(
            DateTime start,
            DateTime end) => start <= nowUtc && nowUtc <= end;
    }

    private async Task SetActiveSubscription(
        EndUser endUser,
        ProcessedSubscription subscription)
    {
        endUser.SubscriptionStartDate = subscription.StartDate;
        endUser.SubscriptionEndDate = subscription.EndDate;
        endUser.IsPaused = false;
        endUser.IsStopped = false;
        endUser.CurrentPauseStartDate = null;
        endUser.CurrentPauseEndDate = null;
        endUser.StopReason = null;
        endUser.StoppedDate = null;
    }

    private async Task SetPausedSubscription(
        EndUser endUser,
        ProcessedSubscription subscription)
    {
        var nowUtc = DateTime.UtcNow;

        endUser.SubscriptionStartDate = subscription.StartDate;
        endUser.SubscriptionEndDate = subscription.EndDate;

        // Check if currently in pause window
        bool IsCurrentlyInPauseWindow()
        {
            if (!subscription.IsPaused || subscription.PausedAt == null) return false;
            if (subscription.ResumedAt == null) return nowUtc >= subscription.PausedAt.Value;
            return nowUtc >= subscription.PausedAt.Value && nowUtc < subscription.ResumedAt.Value;
        }

        if (IsCurrentlyInPauseWindow())
        {
            if (subscription.ResumedAt == null)
            {
                // Indefinitely paused - mark as stopped
                endUser.IsStopped = true;
                endUser.IsPaused = false;
                endUser.StoppedDate = subscription.PausedAt;
                endUser.StopReason = "Subscription paused indefinitely via Rekaz";
                endUser.CurrentPauseStartDate = null;
                endUser.CurrentPauseEndDate = null;
            }
            else
            {
                // Temporarily paused with resume date
                var endDateAfterPause = SubscriptionPredicates.GetNewEndDateAfterPause(
                    endUser,
                    subscription.PausedAt!.Value,
                    subscription.ResumedAt!.Value);
                endUser.IsPaused = true;
                endUser.IsStopped = false;
                endUser.CurrentPauseStartDate = subscription.PausedAt;
                endUser.CurrentPauseEndDate = subscription.ResumedAt;
                endUser.StopReason = null;
                endUser.StoppedDate = null;
                endUser.SubscriptionEndDate = endDateAfterPause;
                // Create pause record
                await CreatePauseRecordIfNeeded(endUser, subscription.PausedAt.Value, subscription.ResumedAt.Value);
            }
        }
        else
        {
            // Not currently in pause window
            endUser.IsPaused = false;
            endUser.IsStopped = false;
            endUser.CurrentPauseStartDate = null;
            endUser.CurrentPauseEndDate = null;
            endUser.StopReason = null;
            endUser.StoppedDate = null;
        }
    }

    private async Task SetUpcomingSubscription(
        EndUser endUser,
        ProcessedSubscription subscription)
    {
        endUser.SubscriptionStartDate = subscription.StartDate;
        endUser.SubscriptionEndDate = subscription.EndDate;
        endUser.IsPaused = false;
        endUser.IsStopped = false;
        endUser.CurrentPauseStartDate = null;
        endUser.CurrentPauseEndDate = null;
        endUser.StopReason = null;
        endUser.StoppedDate = null;
    }

    private async Task ClearActiveSubscription(
        EndUser endUser)
    {
        // Keep historical subscription dates but clear active states
        if (endUser.IsStopped || endUser.IsStoppedByWarning) return;
        endUser.IsPaused = false;
        endUser.IsStopped = false;
        endUser.CurrentPauseStartDate = null;
        endUser.CurrentPauseEndDate = null;
        endUser.StopReason = null;
        endUser.StoppedDate = null;

        // Note: Preserving SubscriptionStartDate/EndDate as historical data
    }

    private async Task CreatePauseRecordIfNeeded(
        EndUser endUser,
        DateTime pauseStart,
        DateTime pauseEnd)
    {
        // Check if pause record already exists
        var existingPause = await context.Set<SubscriptionPause>()
            .AnyAsync(sp => sp.EndUserId == endUser.Id &&
                            sp.PauseStartDate == pauseStart &&
                            sp.PauseEndDate == pauseEnd &&
                            sp.IsActive);

        if (!existingPause)
        {
            var pauseRecord = new SubscriptionPause
            {
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = "System-Rekaz",
                EndUserId = endUser.Id,
                IsActive = true,
                PauseStartDate = pauseStart,
                PauseEndDate = pauseEnd,
                PauseDays = (pauseEnd - pauseStart).Days + 1,
                Reason = "Auto-created from Rekaz sync"
            };

            context.Add(pauseRecord);
        }
    }

    private DateTime NormalizeDate(
        DateTime date)
    {
        // If time is exactly 21:00:00, move to next day at midnight
        if (date.TimeOfDay == new TimeSpan(21, 0, 0))
            return date.Date.AddDays(1);

        return date.Date;
    }

    // Helper class for processed subscription data
    private class ProcessedSubscription
    {
        public Guid RekazId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public decimal PaidAmount { get; set; }
        public bool IsFullyPaid { get; set; }
        public bool IsPaused { get; set; }
        public DateTime? PausedAt { get; set; }
        public DateTime? ResumedAt { get; set; }
    }

    #endregion


    #region Webhook

    public async Task<bool> ProcessWebhookEvent(
        WebhookEvent webhookEvent)
    {
        try
        {
            var data = webhookEvent.Data;
            var customerId = data.Customer?.Id ?? data.FromCustomer.Id;

            logger.LogInformation("Webhook event received {@webhookEvent}", webhookEvent);
            logger.LogInformation("Webhook event {eventName}, status: {status}", webhookEvent.EventName,
                webhookEvent.Data.Status);


            // 1) Find EndUser
            var endUser = await context.EndUsers
                .FirstOrDefaultAsync(e => e.RekazId == customerId);

            if (endUser == null)
            {
                // Create EndUser for Created/Activated events
                if (webhookEvent.EventName == "SubscriptionCreatedEvent" ||
                    webhookEvent.EventName == "SubscriptionActivatedEvent")
                {
                    endUser = await CreateEndUserFromWebhook(data);
                }
                else
                {
                    // For other events, EndUser must exist
                    return false;
                }
            }

            // 2) Process based on event type
            switch (webhookEvent.EventName)
            {
                case "SubscriptionCreatedEvent":
                    return await HandleSubscriptionCreated(endUser, data, webhookEvent.CreatedAt);
                case "SubscriptionActivatedEvent":
                    return await HandleSubscriptionActivated(endUser, data, webhookEvent.CreatedAt);
                case "SubscriptionPausedEvent":
                    return await HandleSubscriptionPaused(endUser, data, webhookEvent.CreatedAt);
                case "SubscriptionResumedEvent":
                    return await HandleSubscriptionResumed(endUser, data, webhookEvent.CreatedAt);
                case "SubscriptionCancelledEvent":
                    return await HandleSubscriptionCancelled(endUser, data, webhookEvent.CreatedAt);
                case "SubscriptionExpiredEvent":
                    return await HandleSubscriptionExpired(endUser, data, webhookEvent.CreatedAt);
                case "SubscriptionTransferedEvent":
                    return await HandleSubscriptionTransferred(endUser, data, webhookEvent.CreatedAt);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing webhook event: {EventId}", webhookEvent.Id);
            return false;
        }
    }

    private string GetCustomFieldValue(
        List<WebhookCustomField> customFields,
        string labelToFind,
        bool isImage = false)
    {
        if (customFields == null) return null;

        var field = customFields.FirstOrDefault(cf =>
            cf.Label != null && cf.Label.Contains(labelToFind.Split('-')[0]
                .Trim()));

        if (isImage)
            field = customFields.FirstOrDefault(w => w.Type == "Image");

        return field?.Value?.ToString();
    }

    private async Task<EndUser> CreateEndUserFromWebhook(
        WebhookSubscriptionData data)
    {
        try
        {
            // Extract user data from custom fields
            var fullName = GetCustomFieldValue(data.CustomFields, "الاسم الكامل - Full Name") ??
                           data.Customer.Name ?? "Unknown User";

            var mobileNumber = "+" + data.Customer.MobileNumber;

            var email = GetCustomFieldValue(data.CustomFields, "البريد الالكتروني في بلايتوميك - Playtomic Email") ??
                        data.Customer.Email;

            var imageUrl = GetCustomFieldValue(data.CustomFields, "صورة شخصية واضحة من كميرا الجوال", true);

            // Set default subscription dates (will be updated by event processing)
            var startUtc = NormalizeDate(data.StartDate)
                .EnsureUtc();
            var endUtc = NormalizeDate(data.EndDate)
                .EnsureUtc();

            var (isSuccess, message, endUser) = await endUserService.CreateEndUserAsync(new EndUserViewModel()
            {
                Email = email,
                ImageUrl = imageUrl,
                Name = fullName,
                PhoneNumber = mobileNumber,
                SubscriptionStartDate = startUtc,
                SubscriptionEndDate = endUtc,
                RekazId = data.Customer.Id
            });

            if (isSuccess || message == "An end user with the same phone number or email already exists.")
            {
                logger.LogInformation("Created new EndUser {UserId} for customer {CustomerId}",
                    endUser.Id, data.Customer.Id);
            }
            else
            {
                throw new Exception($"EndUser {endUser.Id} could not be created. {message}");
            }

            return endUser;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating EndUser for customer: {CustomerId}", data.Customer.Id);
            return null;
        }
    }

    private async Task<bool> HandleSubscriptionCreated(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Create/Update subscription record
        await UpsertSubscriptionRecord(endUser.Id, data, data.Status, eventTime);

        // For created events, don't change user state - wait for activation
        logger.LogInformation("Subscription created for user {UserId}, waiting for activation", endUser.Id);

        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();
        if (!IsUserCurrentSubscription(endUser, startUtc, endUtc))
        {
            await RecalculateUserSubscriptionState(endUser);
        }

        return true;
    }

    private async Task<bool> HandleSubscriptionActivated(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Update subscription record
        await UpsertSubscriptionRecord(endUser.Id, data, data.Status, eventTime);

        // Update user state if this subscription is current
        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();
        var nowUtc = DateTime.UtcNow;

        if (data.Status == SubscriptionStatus.Active && startUtc.Date <= nowUtc.Date && nowUtc.Date <= endUtc.Date)
        {
            // This is an active subscription within date range
            await SetUserActiveSubscription(endUser, startUtc, endUtc);
        }
        else if (data.Status == SubscriptionStatus.StartingSoon && startUtc.Date > nowUtc.Date)
        {
            // This is an upcoming subscription
            await SetUserUpcomingSubscription(endUser, startUtc, endUtc);
        }

        await context.SaveChangesAsync();
        return true;
    }

    private async Task<bool> HandleSubscriptionPaused(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Update subscription record
        await UpsertSubscriptionRecord(endUser.Id, data, SubscriptionStatus.Paused, eventTime);

        // Check if this is the user's current subscription
        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();
        var pausedAtUtc = data.PausedAt?.EnsureUtc();
        var resumeAtUtc = data.ResumeAt?.EnsureUtc();

        if (IsUserCurrentSubscription(endUser, startUtc, endUtc))
        {
            if (resumeAtUtc == null)
            {
                // Indefinitely paused - mark as stopped
                endUser.IsStopped = true;
                endUser.IsPaused = false;
                endUser.StoppedDate = pausedAtUtc;
                endUser.StopReason = "Subscription paused indefinitely via Rekaz";
                endUser.CurrentPauseStartDate = null;
                endUser.CurrentPauseEndDate = null;
            }
            else
            {
                // Temporarily paused
                var newEndDate = SubscriptionPredicates.GetNewEndDateAfterPause(
                    endUser, pausedAtUtc!.Value, resumeAtUtc!.Value);

                endUser.IsPaused = true;
                endUser.IsStopped = false;
                endUser.CurrentPauseStartDate = pausedAtUtc;
                endUser.CurrentPauseEndDate = resumeAtUtc;
                endUser.SubscriptionEndDate = newEndDate;
                endUser.StopReason = null;
                endUser.StoppedDate = null;

                // Create pause record
                await CreatePauseRecord(endUser, pausedAtUtc.Value, resumeAtUtc.Value);
            }

            context.Update(endUser);
        }

        await context.SaveChangesAsync();
        return true;
    }

    private async Task<bool> HandleSubscriptionResumed(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Update subscription record to Active
        await UpsertSubscriptionRecord(endUser.Id, data, SubscriptionStatus.Active, eventTime);

        // Check if this is the user's current subscription
        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();

        if (IsUserCurrentSubscription(endUser, startUtc, endUtc))
        {
            // Resume the subscription
            endUser.IsPaused = false;
            endUser.IsStopped = false;
            endUser.CurrentPauseStartDate = null;
            endUser.CurrentPauseEndDate = null;
            endUser.StopReason = null;
            endUser.StoppedDate = null;
            endUser.SubscriptionStartDate = startUtc;
            endUser.SubscriptionEndDate = endUtc;

            context.Update(endUser);
        }

        await context.SaveChangesAsync();
        return true;
    }

    private async Task<bool> HandleSubscriptionCancelled(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Update subscription record
        await UpsertSubscriptionRecord(endUser.Id, data, SubscriptionStatus.Cancelled, eventTime);

        // Check if this was the user's current subscription
        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();

        if (IsUserCurrentSubscription(endUser, startUtc, endUtc))
        {
            // Find next valid subscription or clear state
            await RecalculateUserSubscriptionState(endUser);
        }

        await context.SaveChangesAsync();
        return true;
    }

    private async Task<bool> HandleSubscriptionExpired(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Update subscription record
        await UpsertSubscriptionRecord(endUser.Id, data, SubscriptionStatus.Expired, eventTime);

        // Check if this was the user's current subscription
        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();

        if (IsUserCurrentSubscription(endUser, startUtc, endUtc))
        {
            // Find next valid subscription or clear state
            await RecalculateUserSubscriptionState(endUser);
        }

        await context.SaveChangesAsync();
        return true;
    }

    private async Task<bool> HandleSubscriptionTransferred(
        EndUser endUser,
        WebhookSubscriptionData data,
        DateTime eventTime)
    {
        // Update subscription record
        await UpsertSubscriptionRecord(endUser.Id, data, SubscriptionStatus.Transferred, eventTime);

        // set the user to stop if he does not have any upcoming sub
        await RecalculateUserSubscriptionState(endUser);

        await context.SaveChangesAsync();
        return true;
    }

    private async Task UpsertSubscriptionRecord(
        int endUserId,
        WebhookSubscriptionData data,
        SubscriptionStatus status,
        DateTime eventTime)
    {
        var startUtc = NormalizeDate(data.StartDate)
            .EnsureUtc();
        var endUtc = NormalizeDate(data.EndDate)
            .EnsureUtc();
        var pausedAtUtc = data.PausedAt?.EnsureUtc();
        var resumeAtUtc = data.ResumeAt?.EnsureUtc();

        var dbSub = await context.Set<EndUserSubscription>()
            .FirstOrDefaultAsync(x => x.RekazId == data.Id);

        var totalAmount = data.Price - data.Discount;

        if (dbSub == null)
        {
            // TODO: add check if there is overlap, and send a notification
            dbSub = new EndUserSubscription
            {
                RekazId = data.Id,
                EndUserId = endUserId,
                StartDate = startUtc,
                EndDate = endUtc,
                Status = status,
                Name = data.Name ?? "Unknown Subscription",
                Price = totalAmount,
                Discount = data.Discount,
                IsPaused = status == SubscriptionStatus.Paused,
                PausedAt = pausedAtUtc,
                ResumedAt = resumeAtUtc,
                Code = data.Code,
                CreatedAt = DateTime.UtcNow
            };

            context.Add(dbSub);
        }
        else
        {
            dbSub.StartDate = startUtc;
            dbSub.EndDate = endUtc;
            dbSub.Status = status;
            dbSub.Name = data.Name ?? dbSub.Name;
            dbSub.Price = totalAmount;
            dbSub.Discount = data.Discount;
            dbSub.IsPaused = status == SubscriptionStatus.Paused;
            dbSub.PausedAt = pausedAtUtc;
            dbSub.ResumedAt = resumeAtUtc;
            dbSub.Code = data.Code;
            dbSub.LastModificationDate = DateTime.UtcNow;
            dbSub.TransferredDate = status == SubscriptionStatus.Transferred ? DateTime.UtcNow : null;
            dbSub.TransferredToId = status == SubscriptionStatus.Transferred ? data.ToCustomer?.Id : null;

            context.Update(dbSub);
        }

        await context.SaveChangesAsync();
    }

    private bool IsUserCurrentSubscription(
        EndUser endUser,
        DateTime startDate,
        DateTime endDate)
    {
        // Check if the subscription dates match the user's current subscription
        return endUser.SubscriptionStartDate.Date == startDate.Date && endUser.SubscriptionEndDate.Date == endDate.Date;
    }

    private async Task SetUserActiveSubscription(
        EndUser endUser,
        DateTime startDate,
        DateTime endDate)
    {
        endUser.SubscriptionStartDate = startDate;
        endUser.SubscriptionEndDate = endDate;
        endUser.IsPaused = false;
        endUser.IsStopped = false;
        endUser.CurrentPauseStartDate = null;
        endUser.CurrentPauseEndDate = null;
        endUser.StopReason = null;
        endUser.StoppedDate = null;

        context.Update(endUser);
    }

    private async Task SetUserUpcomingSubscription(
        EndUser endUser,
        DateTime startDate,
        DateTime endDate)
    {
        // Only set if user doesn't have a current active subscription
        var nowUtc = DateTime.UtcNow;
        if (endUser.SubscriptionEndDate.Date < nowUtc.Date || endUser.IsStopped)
        {
            endUser.SubscriptionStartDate = startDate;
            endUser.SubscriptionEndDate = endDate;
            endUser.IsPaused = false;
            endUser.IsStopped = false;
            endUser.CurrentPauseStartDate = null;
            endUser.CurrentPauseEndDate = null;
            endUser.StopReason = null;
            endUser.StoppedDate = null;

            context.Update(endUser);
        }
    }

    private async Task RecalculateUserSubscriptionState(
        EndUser endUser,
        SubscriptionStatus? status = null)
    {
        var nowUtc = DateTime.UtcNow;

        // Find the best available subscription for this user
        var activeSubscription = await context.Set<EndUserSubscription>()
            .Where(s => s.EndUserId == endUser.Id &&
                        s.Status == SubscriptionStatus.Active &&
                        s.StartDate <= nowUtc && s.EndDate >= nowUtc)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();

        if (activeSubscription != null)
        {
            await SetUserActiveSubscription(endUser, activeSubscription.StartDate, activeSubscription.EndDate);
        }
        else
        {
            // Look for upcoming subscriptions
            var upcomingSubscription = await context.Set<EndUserSubscription>()
                .Where(s => s.EndUserId == endUser.Id &&
                            (s.Status == SubscriptionStatus.Pending || s.Status == SubscriptionStatus.StartingSoon) &&
                            s.StartDate > nowUtc)
                .OrderBy(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (upcomingSubscription != null)
            {
                await SetUserUpcomingSubscription(endUser, upcomingSubscription.StartDate,
                    upcomingSubscription.EndDate);
            }
            else
            {
                // Clear subscription state but preserve historical data
                endUser.IsPaused = false;
                endUser.IsStopped = false;
                endUser.CurrentPauseStartDate = null;
                endUser.CurrentPauseEndDate = null;
                endUser.StopReason = null;
                endUser.StoppedDate = null;


                if (status is SubscriptionStatus.Transferred)
                {
                    endUser.IsStopped = true;
                    endUser.StopReason = "Stopped because subscription is transferred";
                }

                context.Update(endUser);
            }
        }
    }

    private async Task CreatePauseRecord(
        EndUser endUser,
        DateTime pauseStart,
        DateTime pauseEnd)
    {
        var existingPause = await context.Set<SubscriptionPause>()
            .AnyAsync(sp => sp.EndUserId == endUser.Id &&
                            sp.PauseStartDate == pauseStart &&
                            sp.PauseEndDate == pauseEnd &&
                            sp.IsActive);

        if (!existingPause)
        {
            var pauseRecord = new SubscriptionPause
            {
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = "System-Webhook",
                EndUserId = endUser.Id,
                IsActive = true,
                PauseStartDate = pauseStart,
                PauseEndDate = pauseEnd,
                PauseDays = (pauseEnd - pauseStart).Days + 1,
                Reason = "Auto-created from webhook event"
            };

            context.Add(pauseRecord);
        }
    }

    private bool IsValidWebhookEvent(
        WebhookEvent webhookEvent)
    {
        if (webhookEvent?.Data == null)
            return false;

        if (webhookEvent.Data.Id == Guid.Empty)
            return false;

        if (webhookEvent.Data.Customer?.Id == Guid.Empty)
            return false;

        if (webhookEvent.Data.StartDate == default || webhookEvent.Data.EndDate == default)
            return false;

        var validEventNames = new[]
        {
            "SubscriptionCreatedEvent",
            "SubscriptionActivatedEvent",
            "SubscriptionPausedEvent",
            "SubscriptionResumedEvent",
            "SubscriptionCancelledEvent",
            "SubscriptionExpiredEvent"
        };

        return validEventNames.Contains(webhookEvent.EventName);
    }

    #endregion
}