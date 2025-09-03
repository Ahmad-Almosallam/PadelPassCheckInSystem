using Microsoft.EntityFrameworkCore;
using Moq;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Services;
using Xunit;

namespace PadelPassCheckInSystem.Tests.Services.CheckIns;

public class CheckInServiceValidateTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IWarningService> _warningServiceMock;
    private readonly CheckInService _checkInService;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public CheckInServiceValidateTests()
    {
        // Setup in-memory database
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid()
                .ToString())
            .Options;

        _context = new ApplicationDbContext(_options);
        _warningServiceMock = new Mock<IWarningService>();
        _checkInService = new CheckInService(_context, _warningServiceMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test branch with KSA timezone
        var branch = new Branch
        {
            Id = 1,
            Name = "Test Branch",
            Address = "Test Address",
            IsActive = true,
            TimeZoneId = "Asia/Riyadh",
            CreatedAt = DateTime.UtcNow
        };

        // Create test users
        var activeUser = new EndUser
        {
            Id = 1,
            Name = "Active User",
            PhoneNumber = "0512345678",
            Email = "active@test.com",
            UniqueIdentifier = "ACTIVE001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-30), // 30 days ago
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30), // 30 days from now
            IsPaused = false,
            IsStopped = false,
            CreatedAt = DateTime.UtcNow
        };

        var pausedUser = new EndUser
        {
            Id = 2,
            Name = "Paused User",
            PhoneNumber = "0512345679",
            Email = "paused@test.com",
            UniqueIdentifier = "PAUSED001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
            IsPaused = true,
            CurrentPauseStartDate = DateTime.UtcNow.AddDays(-5), // Paused 5 days ago
            CurrentPauseEndDate = DateTime.UtcNow.AddDays(5), // Until 5 days from now
            IsStopped = false,
            CreatedAt = DateTime.UtcNow
        };

        var stoppedUser = new EndUser
        {
            Id = 3,
            Name = "Stopped User",
            PhoneNumber = "0512345680",
            Email = "stopped@test.com",
            UniqueIdentifier = "STOPPED001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
            IsPaused = false,
            IsStopped = true,
            StoppedDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow
        };

        var expiredUser = new EndUser
        {
            Id = 4,
            Name = "Expired User",
            PhoneNumber = "0512345681",
            Email = "expired@test.com",
            UniqueIdentifier = "EXPIRED001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-60), // 60 days ago
            SubscriptionEndDate = DateTime.UtcNow.AddDays(-10), // Expired 10 days ago
            IsPaused = false,
            IsStopped = false,
            CreatedAt = DateTime.UtcNow
        };

        // Create branch time slots (9 AM to 11 PM, Monday to Sunday)
        var timeSlots = new List<BranchTimeSlot>();
        for (int day = 0; day < 7; day++)
        {
            timeSlots.Add(new BranchTimeSlot
            {
                Id = day + 1,
                BranchId = 1,
                DayOfWeek = (DayOfWeek)day,
                StartTime = new TimeSpan(9, 0, 0), // 9 AM
                EndTime = new TimeSpan(23, 0, 0), // 11 PM
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.Branches.Add(branch);
        _context.EndUsers.AddRange(activeUser, pausedUser, stoppedUser, expiredUser);
        _context.BranchTimeSlots.AddRange(timeSlots);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ValidateCheckInAsync_UserNotFound_ReturnsInvalid()
    {
        // Arrange
        var identifier = "NONEXISTENT";
        var branchId = 1;
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("User not found", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_BranchNotFound_ReturnsInvalid()
    {
        // Arrange
        var identifier = "ACTIVE001";
        var branchId = 999; // Non-existent branch
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Branch not found", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_BranchInactive_ReturnsInvalid()
    {
        // Arrange
        var inactiveBranch = new Branch
        {
            Id = 2,
            Name = "Inactive Branch",
            IsActive = false,
            TimeZoneId = "Asia/Riyadh"
        };
        _context.Branches.Add(inactiveBranch);
        await _context.SaveChangesAsync();

        var identifier = "ACTIVE001";
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, 2, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Branch is not active", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_BranchNoTimeZone_ReturnsInvalid()
    {
        // Arrange
        var branchNoTz = new Branch
        {
            Id = 3,
            Name = "No TimeZone Branch",
            IsActive = true,
            TimeZoneId = null
        };
        _context.Branches.Add(branchNoTz);
        await _context.SaveChangesAsync();

        var identifier = "ACTIVE001";
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, 3, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Branch time zone not set", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_UserStopped_ReturnsInvalid()
    {
        // Arrange
        var identifier = "STOPPED001";
        var branchId = 1;
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Subscription is currently stopped by admin", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_UserPaused_ReturnsInvalid()
    {
        // Arrange
        var identifier = "PAUSED001";
        var branchId = 1;
        var checkInDate = DateTime.UtcNow; // Today should be within pause period

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Subscription was paused", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_SubscriptionExpired_ReturnsInvalid()
    {
        // Arrange
        var identifier = "EXPIRED001";
        var branchId = 1;
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Subscription is not active", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_SubscriptionNotStarted_ReturnsInvalid()
    {
        // Arrange
        var futureUser = new EndUser
        {
            Id = 5,
            Name = "Future User",
            PhoneNumber = "0512345682",
            UniqueIdentifier = "FUTURE001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(10), // Starts in 10 days
            SubscriptionEndDate = DateTime.UtcNow.AddDays(40),
            IsPaused = false,
            IsStopped = false
        };
        _context.EndUsers.Add(futureUser);
        await _context.SaveChangesAsync();

        var identifier = "FUTURE001";
        var branchId = 1;
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Subscription is not active", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_AlreadyCheckedInToday_ReturnsInvalid()
    {
        // Arrange
        var identifier = "ACTIVE001";
        var branchId = 1;
        var checkInDate = DateTime.UtcNow;

        // Create existing check-in for today
        var existingCheckIn = new CheckIn
        {
            Id = 1,
            EndUserId = 1,
            BranchId = 1,
            CheckInDateTime = DateTime.UtcNow.AddHours(-2) // 2 hours ago today
        };
        _context.CheckIns.Add(existingCheckIn);
        await _context.SaveChangesAsync();

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("already checked in", result.Message);
        Assert.Null(result.User);
    }

    [Theory]
    [InlineData(4, 0)] // 4 AM - too early
    [InlineData(23, 30)] // 11:30 PM - too late
    public async Task ValidateCheckInAsync_OutsideTimeSlot_ReturnsInvalid(
        int hour,
        int minute)
    {
        // Arrange
        const string identifier = "ACTIVE001";
        const int branchId = 1;

        // Create a specific time outside allowed hours
        var now = DateTime.UtcNow;
        var specificTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);

        // Mock the current time to be outside time slots
        // Note: This test might need adjustment based on how the service gets "now"
        // For this test, we're assuming the check-in time represents the current time

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, specificTime);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Check-in is only allowed during", result.Message);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_ValidActiveUser_ReturnsValid()
    {
        // Arrange
        var identifier = "ACTIVE001";
        var branchId = 1;

        // Set a time that's within business hours (10 AM)
        var now = DateTime.UtcNow;
        var businessHourTime = new DateTime(now.Year, now.Month, now.Day, 10, 0, 0, DateTimeKind.Utc);
        var checkInDate = businessHourTime;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Validation successful", result.Message);
        Assert.NotNull(result.User);
        Assert.Equal("Active User", result.User.Name);
    }

    [Fact]
    public async Task ValidateCheckInAsync_ValidUserByPhoneNumber_ReturnsValid()
    {
        // Arrange
        var identifier = "0512345678"; // Phone number instead of unique identifier
        var branchId = 1;

        var now = DateTime.UtcNow;
        var businessHourTime = new DateTime(now.Year, now.Month, now.Day, 10, 0, 0, DateTimeKind.Utc);
        var checkInDate = businessHourTime;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Validation successful", result.Message);
        Assert.NotNull(result.User);
        Assert.Equal("Active User", result.User.Name);
    }

    [Fact]
    public async Task ValidateCheckInAsync_PauseExpiredUser_AutoUnpausesAndReturnsValid()
    {
        // Arrange - Create a user with expired pause
        var expiredPauseUser = new EndUser
        {
            Id = 6,
            Name = "Expired Pause User",
            PhoneNumber = "0512345683",
            UniqueIdentifier = "EXPIREDPAUSE001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
            IsPaused = true,
            CurrentPauseStartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPauseEndDate = DateTime.UtcNow.AddDays(-2), // Pause ended 2 days ago
            IsStopped = false
        };
        _context.EndUsers.Add(expiredPauseUser);
        await _context.SaveChangesAsync();

        var identifier = "EXPIREDPAUSE001";
        var branchId = 1;

        var now = DateTime.UtcNow;
        var businessHourTime = new DateTime(now.Year, now.Month, now.Day, 10, 0, 0, DateTimeKind.Utc);
        var checkInDate = businessHourTime;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, branchId, checkInDate);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Validation successful", result.Message);
        Assert.NotNull(result.User);

        // Verify user was unpaused
        var updatedUser = await _context.EndUsers.FindAsync(6);
        Assert.False(updatedUser.IsPaused);
        Assert.Null(updatedUser.CurrentPauseStartDate);
        Assert.Null(updatedUser.CurrentPauseEndDate);
    }

    [Fact]
    public async Task ValidateCheckInAsync_NoTimeSlots_AllowsCheckIn()
    {
        // Arrange - Create branch without time slots
        var noTimeSlotsUser = new EndUser
        {
            Id = 7,
            Name = "No Time Slots User",
            PhoneNumber = "0512345684",
            UniqueIdentifier = "NOTIMESLOTS001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
            IsPaused = false,
            IsStopped = false
        };

        var noTimeSletsBranch = new Branch
        {
            Id = 4,
            Name = "No Time Slots Branch",
            IsActive = true,
            TimeZoneId = "Asia/Riyadh"
        };

        _context.EndUsers.Add(noTimeSlotsUser);
        _context.Branches.Add(noTimeSletsBranch);
        await _context.SaveChangesAsync();

        var identifier = "NOTIMESLOTS001";
        var checkInDate = DateTime.UtcNow;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, 4, checkInDate);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Validation successful", result.Message);
        Assert.NotNull(result.User);
    }

    [Fact]
    public async Task ValidateCheckInAsync_MidnightCrossingTimeSlot_ReturnsValid()
    {
        // Arrange - Create time slot that crosses midnight (10 PM to 2 AM)
        var midnightBranch = new Branch
        {
            Id = 5,
            Name = "Midnight Branch",
            IsActive = true,
            TimeZoneId = "Asia/Riyadh"
        };

        var midnightTimeSlot = new BranchTimeSlot
        {
            Id = 8,
            BranchId = 5,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(22, 0, 0), // 10 PM
            EndTime = new TimeSpan(2, 0, 0), // 2 AM next day
            IsActive = true
        };

        _context.Branches.Add(midnightBranch);
        _context.BranchTimeSlots.Add(midnightTimeSlot);
        await _context.SaveChangesAsync();

        var identifier = "ACTIVE001";

        // Test time at 1 AM (which should be within the midnight-crossing slot)
        var now = DateTime.UtcNow;
        var midnightTime = new DateTime(now.Year, now.Month, now.Day, 1, 0, 0, DateTimeKind.Utc);
        var checkInDate = midnightTime;

        // Act
        var result = await _checkInService.ValidateCheckInAsync(identifier, 5, checkInDate);

        // Assert
        // Note: The result depends on the current day of week and implementation details
        // This test might need adjustment based on the exact logic for midnight crossing
        Assert.True(result.IsValid);
    }


    [Fact]
    public async Task ValidateCheckInAsync_MultipleCheckInsOnDifferentDays_AllowsCheckIn()
    {
        // Arrange
        var branch = CreateTestBranch();
        var user = CreateTestUser();
        _context.Branches.Add(branch);
        _context.EndUsers.Add(user);

        // Create check-in from yesterday
        var yesterdayCheckIn = new CheckIn
        {
            EndUserId = user.Id,
            BranchId = branch.Id,
            CheckInDateTime = DateTime.UtcNow.AddDays(-1)
        };
        _context.CheckIns.Add(yesterdayCheckIn);
        await _context.SaveChangesAsync();

        // Act - Try to check in today
        var result = await _checkInService.ValidateCheckInAsync(
            user.UniqueIdentifier,
            branch.Id,
            DateTime.UtcNow);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Asia/Riyadh")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    public async Task ValidateCheckInAsync_DifferentTimeZones_HandlesCorrectly(
        string timeZoneId)
    {
        // Arrange
        var branch = CreateTestBranch();
        branch.TimeZoneId = timeZoneId;
        var user = CreateTestUser();

        _context.Branches.Add(branch);
        _context.EndUsers.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _checkInService.ValidateCheckInAsync(
            user.UniqueIdentifier,
            branch.Id,
            DateTime.UtcNow);

        // Assert
        // The result will depend on the current time in the specified timezone
        // This test ensures the method doesn't crash with different timezones
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task ValidateCheckInAsync_ConcurrentValidation_HandlesCorrectly()
    {
        // Arrange
        var branch = CreateTestBranch();
        var user = CreateTestUser();
        _context.Branches.Add(branch);
        _context.EndUsers.Add(user);
        await _context.SaveChangesAsync();

        // Act - Simulate concurrent validation requests
        var tasks = new List<Task<(bool IsValid, string Message, EndUser User)>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_checkInService.ValidateCheckInAsync(
                user.UniqueIdentifier,
                branch.Id,
                DateTime.UtcNow));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should have the same validation result
        var firstResult = results[0];
        foreach (var result in results)
        {
            Assert.Equal(firstResult.IsValid, result.IsValid);
            // Message might vary slightly due to timing, but IsValid should be consistent
        }
    }


    [Fact]
    public async Task Rejects_When_CheckIn_At_LocalStart_And_Existing_At_LocalEnd_Tokyo()
    {
        var tz = "Asia/Tokyo";
        var branch = CreateTestBranch();
        branch.TimeZoneId = tz;
        var user = CreateTestUser();
        _context.Branches.Add(branch);
        _context.EndUsers.Add(user);
        await _context.SaveChangesAsync();

        var zone = NodaTime.DateTimeZoneProviders.Tzdb[tz];
        var localDate = new NodaTime.LocalDate(2025, 9, 2);
        var startUtc = localDate.AtStartOfDayInZone(zone)
            .ToInstant()
            .ToDateTimeUtc();
        var endUtc = localDate.PlusDays(1)
            .AtStartOfDayInZone(zone)
            .ToInstant()
            .ToDateTimeUtc()
            .AddTicks(-1);

        _context.CheckIns.Add(new CheckIn { EndUserId = user.Id, BranchId = branch.Id, CheckInDateTime = endUtc });
        await _context.SaveChangesAsync();

        var result = await _checkInService.ValidateCheckInAsync(user.UniqueIdentifier, branch.Id, startUtc);
        Assert.False(result.IsValid); // already checked in same local date
    }

    [Fact]
    public async Task Handles_DST_FallBack_NewYork_OncePerLocalDay()
    {
        var tz = "America/New_York";
        var branch = CreateTestBranch();
        branch.TimeZoneId = tz;
        var user = CreateTestUser();
        _context.Branches.Add(branch);
        _context.EndUsers.Add(user);
        await _context.SaveChangesAsync();

        var zone = NodaTime.DateTimeZoneProviders.Tzdb[tz];
        var local = new NodaTime.LocalDateTime(2025, 11, 2, 1, 30); // fall-back hour
        var CheckInDateTime = zone.AtLeniently(local)
            .ToInstant()
            .ToDateTimeUtc();

        _context.CheckIns.Add(new CheckIn
            { EndUserId = user.Id, BranchId = branch.Id, CheckInDateTime = CheckInDateTime });
        await _context.SaveChangesAsync();

        var secondUtc = zone.AtLeniently(local.PlusMinutes(20))
            .ToInstant()
            .ToDateTimeUtc();
        var result = await _checkInService.ValidateCheckInAsync(user.UniqueIdentifier, branch.Id, secondUtc);

        Assert.False(result.IsValid); // still same local day
    }

    [Fact]
    public async Task ValidateCheckInAsync_RiyadhPrevDay_DubaiToday_Allowed_GlobalPerLocalDay()
    {
        // Arrange
        var dubaiBranch = new Branch
            { Id = 11, Name = "Dubai", IsActive = true, TimeZoneId = "Asia/Dubai", CreatedAt = DateTime.UtcNow };
        _context.Branches.Add(dubaiBranch);
        // slots 9–23 daily
        _context.BranchTimeSlots.AddRange(Enumerable.Range(0, 7)
            .Select(d => new BranchTimeSlot
            {
                Id = 300 + d, BranchId = 11, DayOfWeek = (DayOfWeek)d, StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(23, 0, 0), IsActive = true
            }));

        var user = new EndUser
        {
            Id = 101, Name = "GlobalUser", PhoneNumber = "0500000000", UniqueIdentifier = "GLOBAL1",
            SubscriptionStartDate = DateTime.UtcNow.AddYears(-1), SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
        };
        _context.EndUsers.Add(user);
        await _context.SaveChangesAsync();

        var tzdb = NodaTime.DateTimeZoneProviders.Tzdb;
        var riyadh = tzdb["Asia/Riyadh"]; // UTC+3
        var dubai = tzdb["Asia/Dubai"]; // UTC+4

        // Existing check-in in Riyadh: 2025-09-02 22:00 (+03) → 19:00 UTC → Dubai local 23:00 on 2025-09-02
        var riyadhLocal = new NodaTime.LocalDateTime(2025, 9, 2, 22, 0);
        var riyadhUtc = riyadh.AtStrictly(riyadhLocal)
            .ToInstant()
            .ToDateTimeUtc();
        _context.CheckIns.Add(new CheckIn
            { EndUserId = user.Id, BranchId = 1, CheckInDateTime = riyadhUtc }); // different branch OK
        await _context.SaveChangesAsync();

        // Request in Dubai next local day: 2025-09-03 10:00 (+04) → different Dubai local date
        var dubaiLocal = new NodaTime.LocalDateTime(2025, 9, 3, 10, 0);
        var dubaiUtc = dubai.AtStrictly(dubaiLocal)
            .ToInstant()
            .ToDateTimeUtc();

        // Act
        var result = await _checkInService.ValidateCheckInAsync("GLOBAL1", dubaiBranch.Id, dubaiUtc);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Validation successful", result.Message);
    }

    [Fact]
    public async Task ValidateCheckInAsync_RiyadhAndDubai_SameDubaiLocalDay_Blocked_GlobalPerLocalDay()
    {
        // Arrange
        var dubaiBranch = new Branch
            { Id = 12, Name = "Dubai2", IsActive = true, TimeZoneId = "Asia/Dubai", CreatedAt = DateTime.UtcNow };
        _context.Branches.Add(dubaiBranch);
        _context.BranchTimeSlots.AddRange(Enumerable.Range(0, 7)
            .Select(d => new BranchTimeSlot
            {
                Id = 400 + d, BranchId = 12, DayOfWeek = (DayOfWeek)d, StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(23, 0, 0), IsActive = true
            }));

        var user = new EndUser
        {
            Id = 102, Name = "GlobalUser2", PhoneNumber = "0500000001", UniqueIdentifier = "GLOBAL2",
            SubscriptionStartDate = DateTime.UtcNow.AddYears(-1), SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
        };
        _context.EndUsers.Add(user);
        await _context.SaveChangesAsync();

        var tzdb = NodaTime.DateTimeZoneProviders.Tzdb;
        var riyadh = tzdb["Asia/Riyadh"]; // UTC+3
        var dubai = tzdb["Asia/Dubai"]; // UTC+4

        // Existing in Riyadh: 2025-09-03 00:30 Riyadh → 21:30 UTC (prev day) → Dubai local 01:30 on 2025-09-03
        var riyadhLocal = new NodaTime.LocalDateTime(2025, 9, 3, 0, 30);
        var riyadhUtc = riyadh.AtStrictly(riyadhLocal)
            .ToInstant()
            .ToDateTimeUtc();
        _context.CheckIns.Add(new CheckIn { EndUserId = user.Id, BranchId = 1, CheckInDateTime = riyadhUtc });
        await _context.SaveChangesAsync();

        // Request in Dubai same Dubai local day: 2025-09-03 10:00 Dubai
        var dubaiLocal = new NodaTime.LocalDateTime(2025, 9, 3, 10, 0);
        var dubaiUtc = dubai.AtStrictly(dubaiLocal)
            .ToInstant()
            .ToDateTimeUtc();

        // Act
        var result = await _checkInService.ValidateCheckInAsync("GLOBAL2", dubaiBranch.Id, dubaiUtc);

        // Assert: blocked because already checked in on the same **Dubai** local date globally
        Assert.False(result.IsValid);
        Assert.Contains("already checked in", result.Message, StringComparison.OrdinalIgnoreCase);
    }


    private Branch CreateTestBranch()
    {
        return new Branch
        {
            Name = "Test Branch",
            IsActive = true,
            TimeZoneId = "Asia/Riyadh",
            CreatedAt = DateTime.UtcNow
        };
    }

    private EndUser CreateTestUser()
    {
        return new EndUser
        {
            Name = "Test User",
            PhoneNumber = "0512345678",
            UniqueIdentifier = "TEST001",
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
            IsPaused = false,
            IsStopped = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}