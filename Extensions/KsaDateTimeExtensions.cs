namespace PadelPassCheckInSystem.Extensions
{
    /// <summary>
    /// Simple extension methods for converting DateTime to KSA timezone for display purposes
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
        public static DateTime ToKSATime(
            this DateTime utcDateTime)
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
        /// Converts nullable UTC DateTime to KSA time for display
        /// </summary>
        /// <param name="utcDateTime">Nullable UTC DateTime from database</param>
        /// <returns>Nullable DateTime in KSA timezone</returns>
        public static DateTime? ToKSATime(
            this DateTime? utcDateTime)
        {
            if (!utcDateTime.HasValue)
                return null;

            return utcDateTime.Value.ToKSATime();
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
        /// Formats DateTime for KSA display with timezone indicator
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime</param>
        /// <param name="format">Format string</param>
        /// <returns>Formatted string with KSA timezone</returns>
        public static string ToKSAString(
            this DateTime utcDateTime,
            string format = "yyyy-MM-dd HH:mm:ss")
        {
            return utcDateTime.ToKSATime()
                .ToString(format) + " KSA";
        }

        /// <summary>
        /// Formats nullable DateTime for KSA display with timezone indicator
        /// </summary>
        /// <param name="utcDateTime">Nullable UTC DateTime</param>
        /// <param name="format">Format string</param>
        /// <returns>Formatted string with KSA timezone or empty string if null</returns>
        public static string ToKSAString(
            this DateTime? utcDateTime,
            string format = "yyyy-MM-dd HH:mm:ss")
        {
            if (!utcDateTime.HasValue)
                return string.Empty;

            return utcDateTime.Value.ToKSAString(format);
        }

        /// <summary>
        /// Converts KSA DateTime input to UTC for storage
        /// </summary>
        /// <param name="ksaDateTime">DateTime in KSA timezone</param>
        /// <returns>UTC DateTime for database storage</returns>
        public static DateTime ToUTCFromKSA(
            this DateTime ksaDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(ksaDateTime, KSATimeZone);
        }
    }
}