using FluentAssertions;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Tests.ModelTests;

public class DashboardSessionResponseTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    [InlineData(null)]
    public void BuyWindows_MatchSessionDetailedResponse(int? buyDayMinimum)
    {
        // Arrange: the dashboard decides "Buy a Spot" from these, so they must never drift from the session page's
        var sessionDate = new DateTime(2025, 2, 25, 7, 30, 0);
        var detailed = new SessionDetailedResponse { SessionId = 1, CreateDateTime = sessionDate, UpdateDateTime = sessionDate, SessionDate = sessionDate, BuyDayMinimum = buyDayMinimum };
        var dashboard = new DashboardSessionResponse { SessionId = 1, CreateDateTime = sessionDate, UpdateDateTime = sessionDate, SessionDate = sessionDate, BuyDayMinimum = buyDayMinimum };

        // Act / Assert
        dashboard.BuyWindow.Should().Be(detailed.BuyWindow);
        dashboard.BuyWindowPreferred.Should().Be(detailed.BuyWindowPreferred);
        dashboard.BuyWindowPreferredPlus.Should().Be(detailed.BuyWindowPreferredPlus);
    }

    [Fact]
    public void NewSession_DefaultsToEmptyDetailCollections()
    {
        // Act
        var session = new DashboardSessionResponse { SessionId = 1, CreateDateTime = DateTime.UtcNow, UpdateDateTime = DateTime.UtcNow, SessionDate = DateTime.UtcNow };

        // Assert
        session.CurrentRosters.Should().BeEmpty();
        session.BuySells.Should().BeEmpty();
        session.BuyingQueues.Should().BeEmpty();
    }
}
