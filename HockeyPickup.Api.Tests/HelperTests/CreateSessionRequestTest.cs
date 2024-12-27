using HockeyPickup.Api.Models.Requests;

namespace HockeyPickup.Api.Tests.HelperTests;

public class CreateSessionRequestTests
{
    private static readonly TimeZoneInfo PacificZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    [Fact]
    public void SessionDate_WhenSetWithUtcDate_ConvertsToPacificTime()
    {
        // Arrange
        var utcDate = new DateTime(2024, 12, 27, 15, 30, 0, DateTimeKind.Utc); // 15:30 UTC
        var request = new CreateSessionRequest
        {
            SessionDate = utcDate,
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m
        };

        // Assert
        Assert.Equal(DateTimeKind.Unspecified, request.SessionDate.Kind);
        var expectedPacificTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, PacificZone);
        Assert.Equal(expectedPacificTime.Hour, request.SessionDate.Hour);
        Assert.Equal(expectedPacificTime.Minute, request.SessionDate.Minute);
    }

    [Fact]
    public void SessionDate_WhenSetWithLocalDate_PreservesPacificTime()
    {
        // Arrange
        var pacificTime = new DateTime(2024, 12, 27, 7, 30, 0, DateTimeKind.Local);
        var request = new CreateSessionRequest
        {
            SessionDate = pacificTime,
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m
        };

        // Assert
        Assert.Equal(DateTimeKind.Unspecified, request.SessionDate.Kind);
        Assert.Equal(7, request.SessionDate.Hour);
        Assert.Equal(30, request.SessionDate.Minute);
    }

    [Fact]
    public void SessionDate_WhenSetWithUnspecifiedDate_PreservesTime()
    {
        // Arrange
        var unspecifiedTime = new DateTime(2024, 12, 27, 7, 30, 0, DateTimeKind.Unspecified);
        var request = new CreateSessionRequest
        {
            SessionDate = unspecifiedTime,
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m
        };

        // Assert
        Assert.Equal(DateTimeKind.Unspecified, request.SessionDate.Kind);
        Assert.Equal(7, request.SessionDate.Hour);
        Assert.Equal(30, request.SessionDate.Minute);
    }
}
