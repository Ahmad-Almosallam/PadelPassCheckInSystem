using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Models.ViewModels;
using PadelPassCheckInSystem.Services;
using Xunit;

namespace PadelPassCheckInSystem.Tests.Services.CheckIns;

public class CheckInServiceEditCheckInTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IWarningService> _warnings;
    private readonly CheckInService _svc;
    private readonly Mock<ILogger<CheckInService>> _loogerMock;
    

    public CheckInServiceEditCheckInTests()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid()
                .ToString())
            .Options;
        _db = new ApplicationDbContext(opts);
        _warnings = new Mock<IWarningService>(MockBehavior.Strict);
        _warnings.Setup(x => x.ProcessPlayerAttendanceAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((false, string.Empty));
        _loogerMock = new Mock<ILogger<CheckInService>>();
        
        _svc = new CheckInService(_db, _warnings.Object,_loogerMock.Object);
        
    }

    public void Dispose() => _db?.Dispose();

    private EndUser AddUser(
        int id = 1)
    {
        var u = new EndUser
        {
            Id = id, Name = "User", UniqueIdentifier = $"U{id}",
            PhoneNumber = $"050{id:D7}",
            SubscriptionStartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SubscriptionEndDate = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };
        _db.EndUsers.Add(u);
        return u;
    }

    private Branch AddBranch(
        int id,
        string tz)
    {
        var b = new Branch { Id = id, Name = $"B{id}", IsActive = true, TimeZoneId = tz, CreatedAt = DateTime.UtcNow };
        _db.Branches.Add(b);
        return b;
    }

    private CheckIn AddCheckIn(
        int id,
        int userId,
        int branchId,
        DateTime utc)
    {
        var c = new CheckIn { Id = id, EndUserId = userId, BranchId = branchId, CheckInDateTime = utc };
        _db.CheckIns.Add(c);
        return c;
    }

    [Fact]
    public async Task EditCheckInAsync_InvalidRequest_ReturnsError()
    {
        var res1 = await _svc.EditCheckInAsync(null); // null request
        Assert.False(res1.Success);

        var res2 = await _svc.EditCheckInAsync(new EditCheckInRequest { CheckInId = 0 });
        Assert.False(res2.Success);
    }

    [Fact]
    public async Task EditCheckInAsync_CheckInNotFound_ReturnsError()
    {
        var res = await _svc.EditCheckInAsync(new EditCheckInRequest
        {
            CheckInId = 999, CheckInDate = DateTime.UtcNow
        });
        Assert.False(res.Success);
        Assert.Contains("not found", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditCheckInAsync_BranchTimeZoneMissing_ReturnsError()
    {
        var user = AddUser(10);
        var branch = AddBranch(10, null);
        var ci = AddCheckIn(1000, user.Id, branch.Id, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var res = await _svc.EditCheckInAsync(new EditCheckInRequest
        {
            CheckInId = ci.Id,
            CheckInDate = DateTime.UtcNow
        });

        Assert.False(res.Success);
        Assert.Contains("time zone", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditCheckInAsync_Success_UpdatesFields_AndSaves()
    {
        var user = AddUser(20);
        var branch = AddBranch(20, "Asia/Riyadh");
        var ci = AddCheckIn(2000, user.Id, branch.Id, new DateTime(2025, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var req = new EditCheckInRequest
        {
            CheckInId = ci.Id,
            BranchCourtId = 7,
            PlayDurationMinutes = 90,
            PlayStartTime = new DateTime(2025, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            Notes = " updated ",
            PlayerAttended = true,
            CheckInDate = new DateTime(2025, 9, 2, 8, 0, 0, DateTimeKind.Utc)
        };

        var res = await _svc.EditCheckInAsync(req);

        Assert.True(res.Success);
        var saved = await _db.CheckIns.Include(c => c.EndUser)
            .FirstAsync(c => c.Id == ci.Id);
        Assert.Equal(req.CheckInDate, saved.CheckInDateTime);
        Assert.Equal(req.BranchCourtId, saved.BranchCourtId);
        Assert.Equal(TimeSpan.FromMinutes(90), saved.PlayDuration);
        Assert.Equal(req.PlayStartTime, saved.PlayStartTime);
        Assert.Equal("updated", saved.Notes);
        _warnings.Verify(x => x.ProcessPlayerAttendanceAsync(ci.Id, true), Times.Once);
    }

    [Fact]
    public async Task EditCheckInAsync_AttendanceWarning_MessageAppended()
    {
        var user = AddUser(21);
        var branch = AddBranch(21, "Asia/Riyadh");
        var ci = AddCheckIn(2100, user.Id, branch.Id, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        _warnings.Reset();
        _warnings.Setup(x => x.ProcessPlayerAttendanceAsync(ci.Id, false))
            .ReturnsAsync((false, "Warning: marked absent"));

        var req = new EditCheckInRequest
        {
            CheckInId = ci.Id,
            PlayerAttended = false,
            CheckInDate = DateTime.UtcNow
        };

        var res = await _svc.EditCheckInAsync(req);

        Assert.True(res.Success);
        Assert.Contains("Warning: marked absent", res.Message);
        _warnings.Verify(x => x.ProcessPlayerAttendanceAsync(ci.Id, false), Times.Once);
    }

    [Fact]
    public async Task EditCheckInAsync_Duplicate_SameLocalDay_Global_Blocked()
    {
        // Dubai target local day: 2025-09-03
        var user = AddUser(30);
        var riyadh = AddBranch(1, "Asia/Riyadh");
        var dubai = AddBranch(31, "Asia/Dubai");
        // The check-in we will edit belongs to Dubai branch:
        var toEdit = AddCheckIn(3000, user.Id, dubai.Id, new DateTime(2025, 9, 2, 7, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var tzdb = DateTimeZoneProviders.Tzdb;
        var zoneR = tzdb["Asia/Riyadh"];
        var zoneD = tzdb["Asia/Dubai"];

        // Existing OTHER check-in (any branch) that falls within Dubai local date 2025-09-03.
        // Riyadh 2025-09-03 00:30 (+03) → 2025-09-02 21:30 UTC, which is inside Dubai 2025-09-03 local day.
        var riyadhLocal = new LocalDateTime(2025, 9, 3, 0, 30);
        var existingUtc = zoneR.AtStrictly(riyadhLocal)
            .ToInstant()
            .ToDateTimeUtc();
        AddCheckIn(3001, user.Id, riyadh.Id, existingUtc);
        await _db.SaveChangesAsync();

        // Edit target to Dubai 2025-09-03 10:00 local
        var dubaiLocal = new LocalDateTime(2025, 9, 3, 10, 0);
        var newUtc = zoneD.AtStrictly(dubaiLocal)
            .ToInstant()
            .ToDateTimeUtc();

        var req = new EditCheckInRequest
        {
            CheckInId = toEdit.Id,
            CheckInDate = newUtc,
            PlayerAttended = true
        };

        var res = await _svc.EditCheckInAsync(req);

        Assert.False(res.Success);
        Assert.Contains("check-in on this local date", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditCheckInAsync_RiyadhPrevDay_DubaiNextDay_Allowed()
    {
        var user = AddUser(40);
        var riyadh = AddBranch(1, "Asia/Riyadh");
        var dubai = AddBranch(41, "Asia/Dubai");
        var toEdit = AddCheckIn(4000, user.Id, dubai.Id, new DateTime(2025, 9, 2, 7, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var tzdb = DateTimeZoneProviders.Tzdb;
        var zoneR = tzdb["Asia/Riyadh"];
        var zoneD = tzdb["Asia/Dubai"];

        // Existing check-in in Riyadh on 2025-09-02 22:00 local → 19:00 UTC (falls into Dubai 2025-09-02 local day)
        var rLocal = new LocalDateTime(2025, 9, 2, 22, 0);
        var rUtc = zoneR.AtStrictly(rLocal)
            .ToInstant()
            .ToDateTimeUtc();
        AddCheckIn(4001, user.Id, riyadh.Id, rUtc);
        await _db.SaveChangesAsync();

        // Edit to Dubai 2025-09-03 10:00 local (next Dubai local day) → should be allowed
        var dLocal = new LocalDateTime(2025, 9, 3, 10, 0);
        var newUtc = zoneD.AtStrictly(dLocal)
            .ToInstant()
            .ToDateTimeUtc();

        var req = new EditCheckInRequest
        {
            CheckInId = toEdit.Id,
            CheckInDate = newUtc,
            PlayerAttended = true
        };

        var res = await _svc.EditCheckInAsync(req);

        Assert.True(res.Success);
        Assert.Contains("updated successfully", res.Message, StringComparison.OrdinalIgnoreCase);
    }
}