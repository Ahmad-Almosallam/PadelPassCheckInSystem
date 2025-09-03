namespace PadelPassCheckInSystem.Extensions
{
    /// <summary>
    /// Extension methods for converting DateTime to KSA timezone for display purposes
    /// and handling KSA time zone conversions properly throughout the application
    /// </summary>
    public static class KSADateTimeExtensions
    {
        // KSA is UTC+3 (no daylight saving time)
        private static readonly TimeZoneInfo KSATimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");

        /// <summary>
        /// Converts UTC DateTime to KSA time for display
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime from database</param>
        /// <returns>DateTime in KSA timezone</returns>
        public static DateTime ToKSATime(this DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                // Assume it's UTC if not specified (database datetimes are usually UTC)
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            if (utcDateTime.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, KSATimeZone);
            }
            // If it's already local time, convert to UTC first then to KSA
            var utc = utcDateTime.ToUniversalTime();
            return TimeZoneInfo.ConvertTimeFromUtc(utc, KSATimeZone);
        }

        /// <summary>
        /// Gets current KSA time
        /// </summary>
        /// <returns>Current DateTime in KSA timezone</returns>
        public static DateTime GetKSANow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KSATimeZone);
        }

        /// <summary>
        /// Gets current KSA date (time set to 00:00:00)
        /// </summary>
        /// <returns>Current Date in KSA timezone</returns>
        public static DateTime GetKSAToday()
        {
            return GetKSANow().Date;
        }

        /// <summary>
        /// Converts KSA DateTime input to UTC for storage
        /// This is the key method for fixing subscription validation issues
        /// </summary>
        /// <param name="ksaDateTime">DateTime in KSA timezone (from user input)</param>
        /// <returns>UTC DateTime for database storage</returns>
        public static DateTime ToUTCFromKSA(this DateTime ksaDateTime)
        {
            // Ensure the DateTime is treated as KSA time
            var ksaTime = DateTime.SpecifyKind(ksaDateTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(ksaTime, KSATimeZone);
        }
        /// <summary>
        /// Gets the start of day in UTC for a given KSA date
        /// Useful for database queries filtering by KSA dates
        /// </summary>
        /// <param name="ksaDate">Date in KSA timezone</param>
        /// <returns>Start of day in UTC</returns>
        public static DateTime GetStartOfKSADayInUTC(this DateTime ksaDate)
        {
            return ksaDate.Date.ToUTCFromKSA();
        }

        /// <summary>
        /// Gets the end of day in UTC for a given KSA date
        /// Useful for database queries filtering by KSA dates
        /// </summary>
        /// <param name="ksaDate">Date in KSA timezone</param>
        /// <returns>End of day in UTC</returns>
        public static DateTime GetEndOfKSADayInUTC(this DateTime ksaDate)
        {
            return ksaDate.Date.AddDays(1).AddTicks(-1).ToUTCFromKSA();
        }
    }
}