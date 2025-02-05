using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Helpers;

public static class TimeZoneUtils
{
    private const string PACIFIC_TIME_ZONE_ID = "America/Los_Angeles";

    public static DateTime UtcToPacific(this DateTime utcDateTime)
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById(PACIFIC_TIME_ZONE_ID);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, pacificZone);
    }

    [ExcludeFromCodeCoverage]
    public static DateTime PacificToUtc(this DateTime pacificDateTime)
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById(PACIFIC_TIME_ZONE_ID);
        return TimeZoneInfo.ConvertTimeToUtc(pacificDateTime, pacificZone);
    }

    public static DateTime GetCurrentPacificTime()
    {
        return DateTime.UtcNow.UtcToPacific();
    }
}