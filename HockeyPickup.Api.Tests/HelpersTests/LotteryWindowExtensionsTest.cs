using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using Xunit;

namespace HockeyPickup.Api.Tests.HelpersTests;

public class LotteryWindowExtensionsTest
{
    private static SessionDetailedResponse CreateSession() => new()
    {
        SessionId = 1,
        SessionDate = new DateTime(2026, 2, 25, 7, 30, 0),
        BuyDayMinimum = 6,
        LotteryEntryWindowMinutes = 30,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow
    };

    [Theory]
    [InlineData(LotteryClass.PreferredPlus)]
    [InlineData(LotteryClass.Preferred)]
    [InlineData(LotteryClass.Standard)]
    public void LotteryEntryOpenFor_MatchesTierProperty(LotteryClass lotteryClass)
    {
        var session = CreateSession();

        var expected = lotteryClass switch
        {
            LotteryClass.PreferredPlus => session.LotteryEntryOpenPreferredPlus,
            LotteryClass.Preferred => session.LotteryEntryOpenPreferred,
            _ => session.LotteryEntryOpenStandard
        };

        session.LotteryEntryOpenFor(lotteryClass).Should().Be(expected);
    }

    [Theory]
    [InlineData(LotteryClass.PreferredPlus)]
    [InlineData(LotteryClass.Preferred)]
    [InlineData(LotteryClass.Standard)]
    public void LotteryDrawFor_MatchesTierProperty(LotteryClass lotteryClass)
    {
        var session = CreateSession();

        var expected = lotteryClass switch
        {
            LotteryClass.PreferredPlus => session.LotteryDrawPreferredPlus,
            LotteryClass.Preferred => session.LotteryDrawPreferred,
            _ => session.LotteryDrawStandard
        };

        session.LotteryDrawFor(lotteryClass).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, LotteryClass.Standard)]
    [InlineData(true, false, LotteryClass.Preferred)]
    [InlineData(false, true, LotteryClass.PreferredPlus)]
    [InlineData(true, true, LotteryClass.PreferredPlus)]
    public void TierOf_ReturnsExpectedTier(bool preferred, bool preferredPlus, LotteryClass expected)
    {
        var user = new AspNetUser { Id = "u", Preferred = preferred, PreferredPlus = preferredPlus };

        user.TierOf().Should().Be(expected);
    }

    [Fact]
    public void TiersBelow_ReturnsLowerTiersInPriorityOrder()
    {
        LotteryClass.PreferredPlus.TiersBelow().Should().Equal(LotteryClass.Preferred, LotteryClass.Standard);
        LotteryClass.Preferred.TiersBelow().Should().Equal(LotteryClass.Standard);
        LotteryClass.Standard.TiersBelow().Should().BeEmpty();
    }

    [Theory]
    [InlineData(LotteryClass.PreferredPlus)]
    [InlineData(LotteryClass.Preferred)]
    [InlineData(LotteryClass.Standard)]
    public void BuyWindowFor_MatchesResponseComputedProperty(LotteryClass lotteryClass)
    {
        // Drift guard: the entity-side static window math must equal the response computed properties.
        var session = CreateSession();

        var expectedBuyWindow = lotteryClass switch
        {
            LotteryClass.PreferredPlus => session.BuyWindowPreferredPlus,
            LotteryClass.Preferred => session.BuyWindowPreferred,
            _ => session.BuyWindow
        };

        LotteryWindowExtensions.BuyWindowFor(session.SessionDate, session.BuyDayMinimum, lotteryClass).Should().Be(expectedBuyWindow);
        LotteryWindowExtensions.DrawTimeFor(session.SessionDate, session.BuyDayMinimum, session.LotteryEntryWindowMinutes, lotteryClass)
            .Should().Be(session.LotteryDrawFor(lotteryClass));
    }
}
