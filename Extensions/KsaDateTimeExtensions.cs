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
        /// Converts nullable UTC DateTime to KSA time for display
        /// </summary>
        /// <param name="utcDateTime">Nullable UTC DateTime from database</param>
        /// <returns>Nullable DateTime in KSA timezone</returns>
        public static DateTime? ToKSATime(this DateTime? utcDateTime)
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
        /// Gets current KSA date (time set to 00:00:00)
        /// </summary>
        /// <returns>Current Date in KSA timezone</returns>
        public static DateTime GetKSAToday()
        {
            return GetKSANow().Date;
        }

        /// <summary>
        /// Formats DateTime for KSA display with timezone indicator
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime</param>
        /// <param name="format">Format string</param>
        /// <returns>Formatted string with KSA timezone</returns>
        public static string ToKSAString(this DateTime utcDateTime, string format = "yyyy-MM-dd HH:mm:ss")
        {
            return utcDateTime.ToKSATime().ToString(format) + " KSA";
        }

        /// <summary>
        /// Formats nullable DateTime for KSA display with timezone indicator
        /// </summary>
        /// <param name="utcDateTime">Nullable UTC DateTime</param>
        /// <param name="format">Format string</param>
        /// <returns>Formatted string with KSA timezone or empty string if null</returns>
        public static string ToKSAString(this DateTime? utcDateTime, string format = "yyyy-MM-dd HH:mm:ss")
        {
            if (!utcDateTime.HasValue)
                return string.Empty;

            return utcDateTime.Value.ToKSAString(format);
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
        /// Converts nullable KSA DateTime input to UTC for storage
        /// </summary>
        /// <param name="ksaDateTime">Nullable DateTime in KSA timezone</param>
        /// <returns>Nullable UTC DateTime for database storage</returns>
        public static DateTime? ToUTCFromKSA(this DateTime? ksaDateTime)
        {
            if (!ksaDateTime.HasValue)
                return null;

            return ksaDateTime.Value.ToUTCFromKSA();
        }

        /// <summary>
        /// Checks if a date (in KSA time) is today in KSA
        /// </summary>
        /// <param name="dateTime">DateTime to check</param>
        /// <returns>True if the date is today in KSA timezone</returns>
        public static bool IsKSAToday(this DateTime dateTime)
        {
            var todayKSA = GetKSAToday();
            var dateKSA = dateTime.ToKSATime().Date;
            return dateKSA == todayKSA;
        }

        /// <summary>
        /// Checks if a UTC datetime represents today in KSA time
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime from database</param>
        /// <returns>True if the UTC datetime converts to today in KSA</returns>
        public static bool IsKSATodayFromUTC(this DateTime utcDateTime)
        {
            var todayKSA = GetKSAToday();
            var dateKSA = utcDateTime.ToKSATime().Date;
            return dateKSA == todayKSA;
        }

        /// <summary>
        /// Converts a date range from KSA to UTC for database queries
        /// </summary>
        /// <param name="ksaDate">Date in KSA timezone</param>
        /// <returns>Tuple of start and end UTC times for the KSA date</returns>
        public static (DateTime StartUtc, DateTime EndUtc) ToUTCDateRange(this DateTime ksaDate)
        {
            var startOfDayKSA = ksaDate.Date; // 00:00:00
            var endOfDayKSA = ksaDate.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
            
            return (
                StartUtc: startOfDayKSA.ToUTCFromKSA(),
                EndUtc: endOfDayKSA.ToUTCFromKSA()
            );
        }

        /// <summary>
        /// Creates a DateTime with KSA date and time components
        /// </summary>
        /// <param name="ksaDate">Date in KSA</param>
        /// <param name="ksaTime">Time in KSA</param>
        /// <returns>Combined DateTime in KSA timezone</returns>
        public static DateTime CombineKSADateTime(DateTime ksaDate, TimeSpan ksaTime)
        {
            return ksaDate.Date.Add(ksaTime);
        }

        /// <summary>
        /// Validates if a subscription is active on a given KSA date
        /// </summary>
        /// <param name="subscriptionStart">Subscription start date (UTC from database)</param>
        /// <param name="subscriptionEnd">Subscription end date (UTC from database)</param>
        /// <param name="checkDate">Date to check (KSA time, defaults to today)</param>
        /// <returns>True if subscription is active on the given date</returns>
        public static bool IsSubscriptionActiveOnKSADate(DateTime subscriptionStart, DateTime subscriptionEnd, DateTime? checkDate = null)
        {
            var ksaDate = (checkDate ?? GetKSANow()).Date;
            var startKSA = subscriptionStart.ToKSATime().Date;
            var endKSA = subscriptionEnd.ToKSATime().Date;
            
            return ksaDate >= startKSA && ksaDate <= endKSA;
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