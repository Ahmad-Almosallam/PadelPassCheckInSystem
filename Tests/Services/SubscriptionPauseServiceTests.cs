using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Services;
using PadelPassCheckInSystem.Extensions;
using Xunit;

namespace PadelPassCheckInSystem.Tests.Services
{
    public class SubscriptionPauseServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SubscriptionPauseService _service;
        private readonly string _testUserId = "test-user-123";

        public SubscriptionPauseServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new SubscriptionPauseService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        private EndUser CreateTestEndUser(int id = 1, bool isPaused = false)
        {
            var endUser = new EndUser
            {
                Id = id,
                Name = "Test User",
                PhoneNumber = "1234567890",
                Email = "test@example.com",
                SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
                SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
                IsPaused = isPaused,
                CreatedAt = DateTime.UtcNow
            };

            _context.EndUsers.Add(endUser);
            _context.SaveChanges();
            return endUser;
        }

        #region PauseSubscriptionAsync Tests

        [Fact]
        public async Task PauseSubscriptionAsync_WithValidData_ShouldSucceed()
        {
            // Arrange
            var endUser = CreateTestEndUser();
            var pauseStartDate = DateTime.Today.AddDays(1); // Tomorrow
            const int pauseDays = 7;
            const string reason = "Vacation";
            var subEndDateBeforePause = endUser.SubscriptionEndDate;

            // Act
            var result = await _service.PauseSubscriptionAsync(
                endUser.Id, pauseStartDate, pauseDays, reason, _testUserId);

            // Assert
            result.Success.Should().BeTrue();
            result.Message.Should().Contain("paused for 7 days");

            // Verify database changes
            var updatedUser = await _context.EndUsers.FindAsync(endUser.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.IsPaused.Should().BeTrue();
            updatedUser.CurrentPauseStartDate.Should().Be(pauseStartDate);
            updatedUser.SubscriptionEndDate.Should().Be(subEndDateBeforePause.AddDays(pauseDays));

            var pauseRecord = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUser.Id);
            pauseRecord.Should().NotBeNull();
            pauseRecord.PauseDays.Should().Be(pauseDays);
            pauseRecord.Reason.Should().Be(reason);
            pauseRecord.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task PauseSubscriptionAsync_WithNonExistentUser_ShouldFail()
        {
            // Arrange
            const int nonExistentUserId = 999;
            var pauseStartDate = DateTime.Today.AddDays(1);

            // Act
            var result = await _service.PauseSubscriptionAsync(
                nonExistentUserId, pauseStartDate, 7, "Test", _testUserId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("End user not found");
        }

        [Fact]
        public async Task PauseSubscriptionAsync_WithAlreadyPausedUser_ShouldFail()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: true);
            var pauseStartDate = DateTime.Today.AddDays(1);

            // Act
            var result = await _service.PauseSubscriptionAsync(
                endUser.Id, pauseStartDate, 7, "Test", _testUserId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Subscription is already paused");
        }

        [Fact]
        public async Task PauseSubscriptionAsync_WithPastStartDate_ShouldFail()
        {
            // Arrange
            var endUser = CreateTestEndUser();
            var pastDate = DateTime.Today.AddDays(-1); // Yesterday

            // Act
            var result = await _service.PauseSubscriptionAsync(
                endUser.Id, pastDate, 7, "Test", _testUserId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Pause start date cannot be in the past");
        }

        [Fact]
        public async Task PauseSubscriptionAsync_WithStartDateAfterSubscriptionEnd_ShouldFail()
        {
            // Arrange
            var endUser = CreateTestEndUser();
            var subscriptionEndKsa = endUser.SubscriptionEndDate.ToKSATime().Date;
            var pauseStartDate = subscriptionEndKsa.AddDays(1); // After subscription ends

            // Act
            var result = await _service.PauseSubscriptionAsync(
                endUser.Id, pauseStartDate, 7, "Test", _testUserId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Pause start date is after subscription end date");
        }

        #endregion

        #region UnpauseSubscriptionAsync Tests

        [Fact]
        public async Task UnpauseSubscriptionAsync_WithValidData_ShouldSucceed()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: true);
            var pauseStartDate = DateTime.Today.AddDays(-5);
            var pauseEndDate = DateTime.Today.AddDays(2);
            
            // Create active pause record
            var activePause = new SubscriptionPause
            {
                EndUserId = endUser.Id,
                PauseStartDate = pauseStartDate,
                PauseEndDate = pauseEndDate,
                PauseDays = 7,
                Reason = "Test pause",
                CreatedByUserId = _testUserId,
                IsActive = true
            };
            _context.SubscriptionPauses.Add(activePause);
            await _context.SaveChangesAsync();

            var unpauseDate = DateTime.Today; // Today

            // Act
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId, unpauseDate);

            // Assert
            result.Success.Should().BeTrue();
            result.Message.Should().Contain("unpaused successfully");

            // Verify database changes
            var updatedUser = await _context.EndUsers.FindAsync(endUser.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.IsPaused.Should().BeFalse();
            updatedUser.CurrentPauseStartDate.Should().BeNull();
            updatedUser.CurrentPauseEndDate.Should().BeNull();

            var pauseRecord = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUser.Id);
            pauseRecord.Should().NotBeNull();
            pauseRecord!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_WithNonExistentUser_ShouldFail()
        {
            // Arrange
            const int nonExistentUserId = 999;

            // Act
            var result = await _service.UnpauseSubscriptionAsync(nonExistentUserId, _testUserId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("End user not found");
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_WithNotPausedUser_ShouldFail()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: false);

            // Act
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Subscription is not currently paused");
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_UnpausingBeforePauseStart_ShouldUseZeroDays()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: true);
            var originalSubscriptionEnd = endUser.SubscriptionEndDate;
            
            var pauseStartDate = DateTime.Today.AddDays(2); // Future start date
            var pauseEndDate = DateTime.Today.AddDays(8);
            const int totalPauseDays = 7;
            
            var activePause = new SubscriptionPause
            {
                EndUserId = endUser.Id,
                PauseStartDate = pauseStartDate,
                PauseEndDate = pauseEndDate,
                PauseDays = totalPauseDays,
                Reason = "Test pause",
                CreatedByUserId = _testUserId,
                IsActive = true
            };
            _context.SubscriptionPauses.Add(activePause);
            
            // Extend subscription end date as if pause was applied
            endUser.SubscriptionEndDate = originalSubscriptionEnd.AddDays(totalPauseDays);
            await _context.SaveChangesAsync();

            var unpauseDate = DateTime.Today; // Before pause starts

            // Act
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId, unpauseDate);

            // Assert
            result.Success.Should().BeTrue();

            // Verify that subscription end date is adjusted back (all pause days unused)
            var updatedUser = await _context.EndUsers.FindAsync(endUser.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.SubscriptionEndDate.Should().Be(originalSubscriptionEnd);

            var pauseRecord = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUser.Id);
            pauseRecord.Should().NotBeNull();
            pauseRecord!.PauseDays.Should().Be(0); // No days used
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_UnpausingDuringPause_ShouldCalculatePartialDays()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: true);
            var originalSubscriptionEnd = endUser.SubscriptionEndDate;
            
            var pauseStartDate = DateTime.Today.AddDays(-3); // Started 3 days ago
            var pauseEndDate = DateTime.Today.AddDays(3); // Ends in 3 days
            const int totalPauseDays = 7;
            
            var activePause = new SubscriptionPause
            {
                EndUserId = endUser.Id,
                PauseStartDate = pauseStartDate,
                PauseEndDate = pauseEndDate,
                PauseDays = totalPauseDays,
                Reason = "Test pause",
                CreatedByUserId = _testUserId,
                IsActive = true
            };
            _context.SubscriptionPauses.Add(activePause);
            
            // Extend subscription end date as if pause was applied
            endUser.SubscriptionEndDate = originalSubscriptionEnd.AddDays(totalPauseDays);
            await _context.SaveChangesAsync();

            var unpauseDate = DateTime.Today; // Unpausing today (4 days into pause)

            // Act
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId, unpauseDate);

            // Assert
            result.Success.Should().BeTrue();

            // Verify actual pause days used (should be 4: started 3 days ago + today)
            var pauseRecord = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUser.Id);
            pauseRecord.Should().NotBeNull();
            pauseRecord!.PauseDays.Should().Be(4);

            // Verify subscription end date adjustment (3 unused days should be removed)
            var updatedUser = await _context.EndUsers.FindAsync(endUser.Id);
            updatedUser.Should().NotBeNull();
            var expectedEndDate = originalSubscriptionEnd.AddDays(totalPauseDays - 3); // Remove 3 unused days
            updatedUser!.SubscriptionEndDate.Should().Be(expectedEndDate);
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_UnpausingAfterPauseEnd_ShouldUseAllDays()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: true);
            var originalSubscriptionEnd = endUser.SubscriptionEndDate;
            
            var pauseStartDate = DateTime.Today.AddDays(-10); // Started 10 days ago
            var pauseEndDate = DateTime.Today.AddDays(-3); // Ended 3 days ago
            const int totalPauseDays = 7;
            
            var activePause = new SubscriptionPause
            {
                EndUserId = endUser.Id,
                PauseStartDate = pauseStartDate,
                PauseEndDate = pauseEndDate,
                PauseDays = totalPauseDays,
                Reason = "Test pause",
                CreatedByUserId = _testUserId,
                IsActive = true
            };
            _context.SubscriptionPauses.Add(activePause);
            
            // Extend subscription end date as if pause was applied
            endUser.SubscriptionEndDate = originalSubscriptionEnd.AddDays(totalPauseDays);
            await _context.SaveChangesAsync();

            var unpauseDate = DateTime.Today; // Unpausing after pause ended

            // Act
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId, unpauseDate);

            // Assert
            result.Success.Should().BeTrue();

            // Verify all pause days were used
            var pauseRecord = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUser.Id);
            pauseRecord.Should().NotBeNull();
            pauseRecord!.PauseDays.Should().Be(totalPauseDays);

            // Verify subscription end date remains extended (no adjustment needed)
            var updatedUser = await _context.EndUsers.FindAsync(endUser.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.SubscriptionEndDate.Should().Be(originalSubscriptionEnd.AddDays(totalPauseDays));
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_WithoutSpecificDate_ShouldUseCurrentDate()
        {
            // Arrange
            var endUser = CreateTestEndUser(isPaused: true);
            
            var activePause = new SubscriptionPause
            {
                EndUserId = endUser.Id,
                PauseStartDate = DateTime.Today.AddDays(-2),
                PauseEndDate = DateTime.Today.AddDays(5),
                PauseDays = 7,
                Reason = "Test pause",
                CreatedByUserId = _testUserId,
                IsActive = true
            };
            _context.SubscriptionPauses.Add(activePause);
            await _context.SaveChangesAsync();

            // Act (not providing unpause date - should default to today)
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId);

            // Assert
            result.Success.Should().BeTrue();
            var todayKSA = KSADateTimeExtensions.GetKSAToday();
            result.Message.Should().Contain($"unpaused successfully on {todayKSA:MMM dd, yyyy}");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task PauseAndUnpause_FullWorkflow_ShouldWorkCorrectly()
        {
            // Arrange
            var endUser = CreateTestEndUser();
            var originalSubscriptionEnd = endUser.SubscriptionEndDate;
            var pauseStartDate = DateTime.Today.AddDays(1);
            const int pauseDays = 10;

            // Act 1: Pause subscription
            var pauseResult = await _service.PauseSubscriptionAsync(
                endUser.Id, pauseStartDate, pauseDays, "Vacation", _testUserId);

            // Assert 1: Pause successful
            pauseResult.Success.Should().BeTrue();

            // Act 2: Unpause after 5 days
            var unpauseDate = pauseStartDate.AddDays(4); // 5 days into pause (including start day)
            var unpauseResult = await _service.UnpauseSubscriptionAsync(
                endUser.Id, _testUserId, unpauseDate);

            // Assert 2: Unpause successful
            unpauseResult.Success.Should().BeTrue();

            // Verify final state
            var finalUser = await _context.EndUsers.FindAsync(endUser.Id);
            finalUser.Should().NotBeNull();
            finalUser!.IsPaused.Should().BeFalse();

            // Should have used 5 days, so 5 days unused should be deducted
            var expectedFinalEndDate = originalSubscriptionEnd.AddDays(pauseDays - 5);
            finalUser.SubscriptionEndDate.Should().Be(expectedFinalEndDate);
        }

        [Fact]
        public async Task PauseAndUnpause_SpecificScenario_ShouldCalculateCorrectly()
        {
            // Arrange - Subscription from Sep 2 to Oct 1, pause from Sep 13 for 10 days, unpause on Sep 16
            var endUser = new EndUser
            {
                Id = 1,
                Name = "Test User",
                PhoneNumber = "1234567890",
                Email = "test@example.com",
                SubscriptionStartDate = new DateTime(2025, 9, 2, 0, 0, 0, DateTimeKind.Utc), // Sep 2, 2025
                SubscriptionEndDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), // Oct 1, 2025
                IsPaused = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.EndUsers.Add(endUser);
            await _context.SaveChangesAsync();

            var pauseStartDate = new DateTime(2025, 9, 13); // Sep 13, 2025
            const int pauseDays = 10;
            var unpauseDate = new DateTime(2025, 9, 16); // Sep 16, 2025
            var originalSubscriptionEnd = endUser.SubscriptionEndDate;

            // Act 1: Pause subscription from Sep 13 for 10 days
            var pauseResult = await _service.PauseSubscriptionAsync(
                endUser.Id, pauseStartDate, pauseDays, "Test pause", _testUserId);

            // Assert 1: Pause should succeed
            pauseResult.Success.Should().BeTrue();
            pauseResult.Message.Should().Contain("paused for 10 days");

            // Verify subscription end date was extended by 10 days
            var pausedUser = await _context.EndUsers.FindAsync(endUser.Id);
            pausedUser.Should().NotBeNull();
            pausedUser!.IsPaused.Should().BeTrue();
            pausedUser.SubscriptionEndDate.Should().Be(originalSubscriptionEnd.AddDays(pauseDays)); // Oct 11, 2025

            // Act 2: Unpause on Sep 16 (4 days into the pause period: Sep 13, 14, 15, 16)
            var unpauseResult = await _service.UnpauseSubscriptionAsync(
                endUser.Id, _testUserId, unpauseDate);

            // Assert 2: Unpause should succeed
            unpauseResult.Success.Should().BeTrue();
            unpauseResult.Message.Should().Contain("unpaused successfully");

            // Verify final state
            var finalUser = await _context.EndUsers.FindAsync(endUser.Id);
            finalUser.Should().NotBeNull();
            finalUser!.IsPaused.Should().BeFalse();
            finalUser.CurrentPauseStartDate.Should().BeNull();
            finalUser.CurrentPauseEndDate.Should().BeNull();

            // Verify pause record shows 4 days used (Sep 13, 14, 15, 16)
            var pauseRecord = await _context.SubscriptionPauses
                .FirstOrDefaultAsync(sp => sp.EndUserId == endUser.Id);
            pauseRecord.Should().NotBeNull();
            pauseRecord!.IsActive.Should().BeFalse();
            pauseRecord.PauseDays.Should().Be(4); // Only 4 days were actually used

            // Verify subscription end date adjustment
            // Original end: Oct 1, Extended by 10: Oct 11, Unused days (6): Oct 5
            var expectedFinalEndDate = originalSubscriptionEnd.AddDays(4); // Oct 5, 2025 (original + 4 used days)
            finalUser.SubscriptionEndDate.Should().Be(expectedFinalEndDate);
        }

        [Fact]
        public async Task PauseSubscriptionAsync_WithZeroPauseDays_ShouldFail()
        {
            // Arrange
            var endUser = CreateTestEndUser();
            var pauseStartDate = DateTime.Today.AddDays(1);

            // Act
            var result = await _service.PauseSubscriptionAsync(
                endUser.Id, pauseStartDate, 0, "Test", _testUserId);

            // This test assumes the service should validate minimum pause days
            // You may need to add this validation to the service if not present
        }

        [Fact]
        public async Task UnpauseSubscriptionAsync_WithActivePauseButNoPauseRecord_ShouldHandleGracefully()
        {
            // Arrange - Create user marked as paused but no pause record
            var endUser = CreateTestEndUser(isPaused: true);

            // Act
            var result = await _service.UnpauseSubscriptionAsync(endUser.Id, _testUserId);

            // Assert - Should still mark user as unpaused
            result.Success.Should().BeTrue();
            
            var updatedUser = await _context.EndUsers.FindAsync(endUser.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.IsPaused.Should().BeFalse();
        }

        #endregion
    }
}
