using NodaTime;

namespace PadelPassCheckInSystem.Extensions;

public static class NodaTimeExtensions
{
    private static readonly IDateTimeZoneProvider ZoneProvider = DateTimeZoneProviders.Tzdb;

    /// <summary>
    /// Converts a UTC DateTime to local time using IANA time zone ID.
    /// </summary>
    /// <param name="utcDateTime">UTC DateTime from DB</param>
    /// <param name="timeZoneId">IANA time zone ID (e.g., "Asia/Riyadh")</param>
    /// <returns>Local DateTime</returns>
    public static DateTime ToLocalTime(
        this DateTime utcDateTime,
        string timeZoneId)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        if (utcDateTime.Kind != DateTimeKind.Utc)
            utcDateTime = utcDateTime.ToUniversalTime();

        if (!ZoneProvider.Ids.Contains(timeZoneId))
            throw new ArgumentException($"Invalid time zone ID: {timeZoneId}");

        var zone = ZoneProvider[timeZoneId];
        var instant = Instant.FromDateTimeUtc(utcDateTime);
        var zonedDateTime = instant.InZone(zone);

        return zonedDateTime.ToDateTimeUnspecified();
    }

    /// <summary>
    /// Converts a local DateTime (from user input) to UTC for DB storage.
    /// </summary>
    public static DateTime ToUtc(
        this DateTime localDateTime,
        string timeZoneId)
    {
        if (!ZoneProvider.Ids.Contains(timeZoneId))
            throw new ArgumentException($"Invalid time zone ID: {timeZoneId}");

        var zone = ZoneProvider[timeZoneId];
        var local = LocalDateTime.FromDateTime(localDateTime);
        var zoned = zone.AtStrictly(local);
        return zoned.ToDateTimeUtc();
    }

    /// <summary>
    /// Gets the current local time for the given time zone.
    /// </summary>
    public static DateTime GetLocalNow(
        string timeZoneId)
    {
        if (!ZoneProvider.Ids.Contains(timeZoneId))
            throw new ArgumentException($"Invalid time zone ID: {timeZoneId}");

        var zone = ZoneProvider[timeZoneId];
        var now = SystemClock.Instance.GetCurrentInstant();
        var zoned = now.InZone(zone);

        return zoned.ToDateTimeUnspecified();
    }

    /// <summary>
    /// Gets the start of the given day in UTC.
    /// </summary>
    public static DateTime GetStartOfDayUtc(
        this DateTime localDate,
        string timeZoneId)
    {
        var zone = ZoneProvider[timeZoneId];
        var local = LocalDate.FromDateTime(localDate);
        var startOfDay = local.AtStartOfDayInZone(zone);
        return startOfDay.ToDateTimeUtc();
    }

    /// <summary>
    /// Gets the end of the given day in UTC.
    /// </summary>
    public static DateTime GetEndOfDayUtc(
        this DateTime localDate,
        string timeZoneId)
    {
        var zone = ZoneProvider[timeZoneId];
        var local = LocalDate.FromDateTime(localDate)
            .PlusDays(1);
        var startOfNextDay = local.AtStartOfDayInZone(zone);
        return startOfNextDay.ToDateTimeUtc()
            .AddTicks(-1);
    }
}