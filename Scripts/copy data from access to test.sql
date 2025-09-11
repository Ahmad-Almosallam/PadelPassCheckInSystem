SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

------------------------------------------------------------
-- 1) Temporarily disable FK checks on target schema [test]
------------------------------------------------------------
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE [test].' + QUOTENAME(t.name) + N' NOCHECK CONSTRAINT ALL;'
FROM sys.tables t
         JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'test';
EXEC sp_executesql @sql;

------------------------------------------------------------
-- 2) Copy in FK-safe order (turn on IDENTITY_INSERT where needed)
------------------------------------------------------------

-- AspNetRoles
INSERT INTO [test].[AspNetRoles] (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT Id, Name, NormalizedName, ConcurrencyStamp
FROM   [access].[AspNetRoles];

-- Branches (identity)
SET IDENTITY_INSERT [test].[Branches] ON;
INSERT INTO [test].[Branches] (Id, Name, Address, IsActive, CreatedAt, PlaytomicTenantId, TimeZoneId)
SELECT Id, Name, Address, IsActive, CreatedAt, PlaytomicTenantId, TimeZoneId
FROM   [access].[Branches];
SET IDENTITY_INSERT [test].[Branches] OFF;

-- AspNetUsers
INSERT INTO [test].[AspNetUsers] (
    Id, FullName, BranchId, UserName, NormalizedUserName, Email, NormalizedEmail,
    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber,
    PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
)
SELECT
    Id, FullName, BranchId, UserName, NormalizedUserName, Email, NormalizedEmail,
    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber,
    PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
FROM [access].[AspNetUsers];

-- AspNetRoleClaims (identity, FK -> Roles)
SET IDENTITY_INSERT [test].[AspNetRoleClaims] ON;
INSERT INTO [test].[AspNetRoleClaims] (Id, RoleId, ClaimType, ClaimValue)
SELECT Id, RoleId, ClaimType, ClaimValue
FROM   [access].[AspNetRoleClaims];
SET IDENTITY_INSERT [test].[AspNetRoleClaims] OFF;

-- AspNetUserClaims (identity, FK -> Users)
SET IDENTITY_INSERT [test].[AspNetUserClaims] ON;
INSERT INTO [test].[AspNetUserClaims] (Id, UserId, ClaimType, ClaimValue)
SELECT Id, UserId, ClaimType, ClaimValue
FROM   [access].[AspNetUserClaims];
SET IDENTITY_INSERT [test].[AspNetUserClaims] OFF;

-- AspNetUserLogins (FK -> Users)
INSERT INTO [test].[AspNetUserLogins] (LoginProvider, ProviderKey, ProviderDisplayName, UserId)
SELECT LoginProvider, ProviderKey, ProviderDisplayName, UserId
FROM   [access].[AspNetUserLogins];

-- AspNetUserRoles (FK -> Users/Roles)
INSERT INTO [test].[AspNetUserRoles] (UserId, RoleId)
SELECT UserId, RoleId
FROM   [access].[AspNetUserRoles];

-- AspNetUserTokens (FK -> Users)
INSERT INTO [test].[AspNetUserTokens] (UserId, LoginProvider, Name, Value)
SELECT UserId, LoginProvider, Name, Value
FROM   [access].[AspNetUserTokens];

-- EndUsers (identity)
SET IDENTITY_INSERT [test].[EndUsers] ON;
INSERT INTO [test].[EndUsers] (
    Id, Name, PhoneNumber, Email, ImageUrl, SubscriptionStartDate, SubscriptionEndDate,
    UniqueIdentifier, QRCodeDownloadToken, HasDownloadedQR, CreatedAt, CurrentPauseEndDate,
    CurrentPauseStartDate, IsPaused, IsStopped, StopReason, StoppedDate, PlaytomicUserId,
    IsStoppedByWarning, WarningCount, RekazId
)
SELECT
    Id, Name, PhoneNumber, Email, ImageUrl, SubscriptionStartDate, SubscriptionEndDate,
    UniqueIdentifier, QRCodeDownloadToken, HasDownloadedQR, CreatedAt, CurrentPauseEndDate,
    CurrentPauseStartDate, IsPaused, IsStopped, StopReason, StoppedDate, PlaytomicUserId,
    IsStoppedByWarning, WarningCount, RekazId
FROM [access].[EndUsers];
SET IDENTITY_INSERT [test].[EndUsers] OFF;

-- EndUserSubscriptions (identity, FK -> EndUsers)
SET IDENTITY_INSERT [test].[EndUserSubscriptions] ON;
INSERT INTO [test].[EndUserSubscriptions] (
    Id, RekazId, EndUserId, StartDate, EndDate, Status, Name, Price, Discount, IsPaused, PausedAt, ResumedAt
)
SELECT
    Id, RekazId, EndUserId, StartDate, EndDate, Status, Name, Price, Discount, IsPaused, PausedAt, ResumedAt
FROM [access].[EndUserSubscriptions];
SET IDENTITY_INSERT [test].[EndUserSubscriptions] OFF;

-- BranchCourts (identity, FK -> Branches)
SET IDENTITY_INSERT [test].[BranchCourts] ON;
INSERT INTO [test].[BranchCourts] (Id, CourtName, BranchId, IsActive, CreatedAt)
SELECT Id, CourtName, BranchId, IsActive, CreatedAt
FROM   [access].[BranchCourts];
SET IDENTITY_INSERT [test].[BranchCourts] OFF;

-- BranchTimeSlots (identity, FK -> Branches)
SET IDENTITY_INSERT [test].[BranchTimeSlots] ON;
INSERT INTO [test].[BranchTimeSlots] (Id, BranchId, StartTime, EndTime, DayOfWeek, IsActive, CreatedAt)
SELECT Id, BranchId, StartTime, EndTime, DayOfWeek, IsActive, CreatedAt
FROM   [access].[BranchTimeSlots];
SET IDENTITY_INSERT [test].[BranchTimeSlots] OFF;

-- CheckIns (identity, FK -> EndUsers/Branches/BranchCourts)
SET IDENTITY_INSERT [test].[CheckIns] ON;
INSERT INTO [test].[CheckIns] (
    Id, EndUserId, BranchId, CheckInDateTime, CourtName, Notes, PlayDuration,
    PlayStartTime, PlayerAttended, BranchCourtId, CreatedAt
)
SELECT
    Id, EndUserId, BranchId, CheckInDateTime, CourtName, Notes, PlayDuration,
    PlayStartTime, PlayerAttended, BranchCourtId, CreatedAt
FROM [access].[CheckIns];
SET IDENTITY_INSERT [test].[CheckIns] OFF;

-- SubscriptionPauses (identity, FK -> EndUsers/AspNetUsers)
SET IDENTITY_INSERT [test].[SubscriptionPauses] ON;
INSERT INTO [test].[SubscriptionPauses] (
    Id, EndUserId, PauseStartDate, PauseDays, PauseEndDate, Reason,
    CreatedByUserId, CreatedAt, IsActive
)
SELECT
    Id, EndUserId, PauseStartDate, PauseDays, PauseEndDate, Reason,
    CreatedByUserId, CreatedAt, IsActive
FROM [access].[SubscriptionPauses];
SET IDENTITY_INSERT [test].[SubscriptionPauses] OFF;

-- PlaytomicIntegrations (identity)
SET IDENTITY_INSERT [test].[PlaytomicIntegrations] ON;
INSERT INTO [test].[PlaytomicIntegrations] (
    Id, AccessToken, AccessTokenExpiration, RefreshToken, RefreshTokenExpiration, CreatedAt, UpdatedAt
)
SELECT
    Id, AccessToken, AccessTokenExpiration, RefreshToken, RefreshTokenExpiration, CreatedAt, UpdatedAt
FROM [access].[PlaytomicIntegrations];
SET IDENTITY_INSERT [test].[PlaytomicIntegrations] OFF;


------------------------------------------------------------
-- 3) Re-enable and validate constraints on target
------------------------------------------------------------
SET @sql = N'';
SELECT @sql += N'ALTER TABLE [test].' + QUOTENAME(t.name) + N' WITH CHECK CHECK CONSTRAINT ALL;'
FROM sys.tables t
         JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'test';
EXEC sp_executesql @sql;

COMMIT TRAN;
