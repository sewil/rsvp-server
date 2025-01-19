using System;
using WzTools.Objects;

namespace WvsBeta.Common
{
    public static class Tools
    {
        public static long GetFileTimeWithAddition(TimeSpan span)
        {
            return (MasterThread.CurrentDate + span).ToFileTimeUtc();
        }


        public static long GetTimeAsMilliseconds(DateTime pNow)
        {
            return pNow.ToFileTime() / 10000;
        }

        public static long GetDateExpireFromPeriodDays(int periodDays)
        {
            return GetFileTimeWithAddition(TimeSpan.FromDays(periodDays));
        }
        public static long GetDateExpireFromPeriodMinutes(int periodMinutes)
        {
            return GetFileTimeWithAddition(TimeSpan.FromMinutes(periodMinutes));
        }

        public static DateTime AsYYYYMMDDHHDateTime(this int val)
        {
            var year = val / 1000000;
            var month = val / 10000 % 100;
            var day = val / 100 % 100;
            var hour = val % 100;

            if (year < 1) throw new Exception($"Passed YYYYMMDDHH is invalid: {val}");

            return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
        }


        public static DateTime GetYYYYMMDDHHDateTime(this WzProperty val, string key, DateTime defaultDateTime = default)
        {
            return val.GetInt32(key)?.AsYYYYMMDDHHDateTime() ?? defaultDateTime;
        }
    }
}
