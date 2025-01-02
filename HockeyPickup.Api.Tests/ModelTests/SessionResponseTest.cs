using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Tests.ModelTests;

public class SessionDetailedResponseTests
{
    [Fact]
    public void BuyWindow_ShouldReturnCorrectDateTime()
    {
        // Arrange
        var sessionDate = new DateTime(2025, 2, 25, 7, 30, 0);
        var buyDayMinimum = 6;
        var expectedBuyWindow = new DateTime(2025, 2, 19, 9, 30, 0);

        var session = new SessionDetailedResponse
        {
            SessionId = 1,
            CreateDateTime = DateTime.Now,
            UpdateDateTime = DateTime.Now,
            SessionDate = sessionDate,
            BuyDayMinimum = buyDayMinimum
        };

        // Act
        var actualBuyWindow = session.BuyWindow;

        // Assert
        Assert.Equal(expectedBuyWindow, actualBuyWindow);
    }

    [Fact]
    public void BuyWindowPreferred_ShouldReturnCorrectDateTime()
    {
        // Arrange
        var sessionDate = new DateTime(2025, 2, 25, 7, 30, 0);
        var buyDayMinimum = 6;
        var expectedBuyWindowPreferred = new DateTime(2025, 2, 18, 9, 30, 0);

        var session = new SessionDetailedResponse
        {
            SessionId = 1,
            CreateDateTime = DateTime.Now,
            UpdateDateTime = DateTime.Now,
            SessionDate = sessionDate,
            BuyDayMinimum = buyDayMinimum
        };

        // Act
        var actualBuyWindowPreferred = session.BuyWindowPreferred;

        // Assert
        Assert.Equal(expectedBuyWindowPreferred, actualBuyWindowPreferred);
    }

    [Fact]
    public void BuyWindowPreferredPlus_ShouldReturnCorrectDateTime()
    {
        // Arrange
        var sessionDate = new DateTime(2025, 2, 25, 7, 30, 0);
        var buyDayMinimum = 6;
        var expectedBuyWindowPreferredPlus = new DateTime(2025, 2, 18, 9, 25, 0);

        var session = new SessionDetailedResponse
        {
            SessionId = 1,
            CreateDateTime = DateTime.Now,
            UpdateDateTime = DateTime.Now,
            SessionDate = sessionDate,
            BuyDayMinimum = buyDayMinimum
        };

        // Act
        var actualBuyWindowPreferredPlus = session.BuyWindowPreferredPlus;

        // Assert
        Assert.Equal(expectedBuyWindowPreferredPlus, actualBuyWindowPreferredPlus);
    }
}
