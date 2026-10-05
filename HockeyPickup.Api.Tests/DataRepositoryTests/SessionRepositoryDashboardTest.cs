using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

public class SessionRepositoryDashboardTests : IDisposable
{
    private const string Viewer = "viewer";
    private const string Other = "other";

    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<HttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<HockeyPickupContext> _options;

    // Day-sized offsets keep every seeded session clearly on one side of "now" whatever the clock says
    private readonly DateTime _now = TimeZoneUtils.GetCurrentPacificTime();
    private int _nextRosterId = 1;

    public SessionRepositoryDashboardTests()
    {
        _mockLogger = new Mock<ILogger<SessionRepository>>();
        _mockContextAccessor = new Mock<HttpContextAccessor>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["SessionBuyPrice"]).Returns("27.00");

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new DetailedSessionTestContext(_options);
        context.Database.EnsureCreated();
        foreach (var userId in new[] { Viewer, Other, "seller", "buyer" })
        {
            context.Users!.Add(new AspNetUser
            {
                Id = userId,
                UserName = $"{userId}@example.com",
                Email = $"{userId}@example.com",
                FirstName = $"{userId}-first",
                LastName = $"{userId}-last",
                NotificationPreference = NotificationPreference.All,
                PositionPreference = PositionPreference.Forward,
                Shoots = ShootPreference.Left
            });
        }
        context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private SessionRepository CreateRepository(HockeyPickupContext context) =>
        new(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

    private async Task<DashboardResponse> GetDashboardAsync()
    {
        await using var context = new DetailedSessionTestContext(_options);
        return await CreateRepository(context).GetDashboardAsync(Viewer);
    }

    private async Task SeedSessionsAsync(params Session[] sessions)
    {
        await using var context = new DetailedSessionTestContext(_options);
        context.Sessions!.AddRange(sessions);
        await context.SaveChangesAsync();
    }

    private Session NewSession(int id, double daysFromNow, string? note = "8/10", int? buyDayMinimum = 6, decimal cost = 0) => new()
    {
        SessionId = id,
        CreateDateTime = _now,
        UpdateDateTime = _now,
        SessionDate = _now.AddDays(daysFromNow),
        Note = note,
        BuyDayMinimum = buyDayMinimum,
        Cost = cost
    };

    private CurrentSessionRoster RosterRow(int sessionId, string userId, PositionPreference position, bool isPlaying, DateTime joined, bool isRegular = false, string? firstName = null) => new()
    {
        SessionRosterId = _nextRosterId++,
        SessionId = sessionId,
        UserId = userId,
        Email = $"{userId}@example.com",
        FirstName = firstName ?? $"{userId}-first",
        LastName = $"{userId}-last",
        TeamAssignment = (int) TeamAssignment.Dark,
        IsPlaying = isPlaying,
        IsRegular = isRegular,
        PlayerStatus = isPlaying ? "Substitute" : "Not Playing",
        Rating = 0,
        PhotoUrl = $"https://photos/{userId}.jpg",
        JoinedDateTime = joined,
        Position = (int) position,
        CurrentPosition = position.ToString()
    };

    private async Task SeedRosterAsync(params CurrentSessionRoster[] rows)
    {
        await using var context = new DetailedSessionTestContext(_options);
        context.CurrentSessionRosters!.AddRange(rows);
        await context.SaveChangesAsync();
    }

    private static BuySell NewBuySell(int id, int sessionId, string? buyerId, string? sellerId, bool paymentSent = false, bool paymentReceived = false, decimal? price = 27m) => new()
    {
        BuySellId = id,
        SessionId = sessionId,
        BuyerUserId = buyerId,
        SellerUserId = sellerId,
        PaymentSent = paymentSent,
        PaymentReceived = paymentReceived,
        Price = price,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow,
        CreateByUserId = Viewer,
        UpdateByUserId = Viewer
    };

    private async Task SeedBuySellsAsync(params BuySell[] buySells)
    {
        await using var context = new DetailedSessionTestContext(_options);
        context.BuySells!.AddRange(buySells);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDashboardAsync_NoSessions_ReturnsEmptyCollections()
    {
        // Act
        var result = await GetDashboardAsync();

        // Assert
        result.UpcomingSessions.Should().BeEmpty();
        result.Sessions.Should().BeEmpty();
        result.PendingPayments.Should().BeEmpty();
        result.GoalieStartsByYear.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_UpcomingSessions_FutureOnlySoonestFirstIncludingCancelled()
    {
        // Arrange
        await SeedSessionsAsync(
            NewSession(1, -3),
            NewSession(2, 9),
            NewSession(3, 2, note: "CANCELLED - ice issue"),
            NewSession(4, 5, note: null));

        // Act
        var result = await GetDashboardAsync();

        // Assert
        result.UpcomingSessions.Select(s => s.SessionId).Should().Equal(3, 4, 2);
        result.UpcomingSessions.Should().OnlyContain(s => s.Cost == 27m);
    }

    [Fact]
    public async Task GetDashboardAsync_Sessions_SkipCancelledAndStopAtDetailLimit()
    {
        // Arrange: a cancelled session up front, then more live sessions than the detail limit
        var sessions = new List<Session> { NewSession(100, 1, note: "Session Cancelled") };
        for (var i = 0; i < SessionRepository.DashboardDetailSessionLimit + 2; i++)
            sessions.Add(NewSession(200 + i, 2 + i, note: i == 0 ? null : "8/10"));
        await SeedSessionsAsync(sessions.ToArray());

        // Act
        var result = await GetDashboardAsync();

        // Assert
        result.UpcomingSessions.Should().HaveCount(SessionRepository.DashboardDetailSessionLimit + 3);
        result.Sessions.Select(s => s.SessionId).Should().Equal(
            Enumerable.Range(200, SessionRepository.DashboardDetailSessionLimit));
    }

    [Fact]
    public async Task GetDashboardAsync_Sessions_CarryBasicFieldsAndBuyWindows()
    {
        // Arrange
        await SeedSessionsAsync(NewSession(1, 10, note: "3/10", buyDayMinimum: 4, cost: 30m));

        // Act
        var session = (await GetDashboardAsync()).Sessions.Single();

        // Assert
        session.SessionId.Should().Be(1);
        session.Note.Should().Be("3/10");
        session.Cost.Should().Be(30m);
        session.BuyDayMinimum.Should().Be(4);
        session.SessionDate.Should().Be(_now.AddDays(10));
        session.CreateDateTime.Should().Be(_now);
        session.UpdateDateTime.Should().Be(_now);
        session.RegularSetId.Should().BeNull();
        session.BuyWindow.Should().Be(_now.AddDays(6).AddHours(2));
        session.CurrentRosters.Should().BeEmpty();
        session.BuySells.Should().BeEmpty();
        session.BuyingQueues.Should().BeEmpty();
        session.Goalies.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_Sessions_RosterInSessionPageOrderWithOnlyItsOwnRows()
    {
        // Arrange
        await SeedSessionsAsync(NewSession(1, 3), NewSession(2, 4));
        var joined = _now.AddDays(-10);
        await SeedRosterAsync(
            RosterRow(1, "buyer", PositionPreference.Forward, true, joined.AddMinutes(5)),
            RosterRow(1, "seller", PositionPreference.Forward, false, joined),
            RosterRow(1, Viewer, PositionPreference.Defense, true, joined.AddMinutes(9), isRegular: true),
            RosterRow(2, Other, PositionPreference.Forward, true, joined));

        // Act
        var session = (await GetDashboardAsync()).Sessions.First(s => s.SessionId == 1);

        // Assert: regulars first, then by position descending, then by when they joined
        session.CurrentRosters.Select(r => r.UserId).Should().Equal(Viewer, "seller", "buyer");
        var viewer = session.CurrentRosters.First();
        viewer.FirstName.Should().Be("viewer-first");
        viewer.LastName.Should().Be("viewer-last");
        viewer.TeamAssignment.Should().Be(TeamAssignment.Dark);
        viewer.Position.Should().Be(PositionPreference.Defense);
        viewer.CurrentPosition.Should().Be("Defense");
        viewer.IsPlaying.Should().BeTrue();
        session.CurrentRosters.Single(r => r.UserId == "seller").IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task GetDashboardAsync_Sessions_GoaliesArePlayingGoaliesByJoinedThenRosterOrder()
    {
        // Arrange: two goalies joined at the same instant; the roster sorts them by first name
        await SeedSessionsAsync(NewSession(1, 3));
        var joined = _now.AddDays(-10);
        await SeedRosterAsync(
            RosterRow(1, "buyer", PositionPreference.Goalie, true, joined, firstName: "Zed"),
            RosterRow(1, Other, PositionPreference.Goalie, true, joined, firstName: "Amy"),
            RosterRow(1, "seller", PositionPreference.Goalie, false, joined.AddMinutes(-5)),
            RosterRow(1, Viewer, PositionPreference.Forward, true, joined.AddMinutes(-9)));

        // Act
        var session = (await GetDashboardAsync()).Sessions.Single();

        // Assert
        session.Goalies.Select(g => g.UserId).Should().Equal(Other, "buyer");
        var first = session.Goalies.First();
        first.FirstName.Should().Be("Amy");
        first.LastName.Should().Be("other-last");
        first.PhotoUrl.Should().Be("https://photos/other.jpg");
        first.IsPlaying.Should().BeTrue();
        first.JoinedDateTime.Should().Be(joined);
        session.CurrentRosters.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetDashboardAsync_Sessions_BuySellsWithCounterpartiesAndQueueRows()
    {
        // Arrange
        await SeedSessionsAsync(NewSession(1, 3), NewSession(2, 4));
        await SeedBuySellsAsync(
            NewBuySell(12, 1, "buyer", "seller", paymentSent: true, price: null),
            NewBuySell(11, 1, null, "seller"),
            NewBuySell(13, 1, Viewer, null),
            NewBuySell(14, 2, Other, null));
        await using (var seed = new DetailedSessionTestContext(_options))
        {
            seed.SessionBuyingQueues!.AddRange(
                new BuyingQueue { BuySellId = 13, SessionId = 1, BuyerUserId = Viewer, TransactionStatus = "Looking to Buy", QueueStatus = "In Queue (2)" },
                new BuyingQueue { BuySellId = 11, SessionId = 1, SellerUserId = "seller", TransactionStatus = "Available to Buy", QueueStatus = "Next in Line" },
                new BuyingQueue { BuySellId = 14, SessionId = 2, BuyerUserId = Other, TransactionStatus = "Looking to Buy", QueueStatus = "Next in Line" });
            await seed.SaveChangesAsync();
        }

        // Act
        var session = (await GetDashboardAsync()).Sessions.First(s => s.SessionId == 1);

        // Assert
        session.BuySells.Select(b => b.BuySellId).Should().Equal(11, 12, 13);
        var completed = session.BuySells.Single(b => b.BuySellId == 12);
        completed.SessionId.Should().Be(1);
        completed.BuyerUserId.Should().Be("buyer");
        completed.SellerUserId.Should().Be("seller");
        completed.PaymentSent.Should().BeTrue();
        completed.PaymentReceived.Should().BeFalse();
        completed.Price.Should().Be(0m);
        completed.Buyer.Should().BeEquivalentTo(new { Id = "buyer", FirstName = "buyer-first", LastName = "buyer-last" });
        completed.Seller.Should().BeEquivalentTo(new { Id = "seller", FirstName = "seller-first", LastName = "seller-last" });
        var listing = session.BuySells.Single(b => b.BuySellId == 11);
        listing.Buyer.Should().BeNull();
        listing.Price.Should().Be(27m);
        session.BuySells.Single(b => b.BuySellId == 13).Seller.Should().BeNull();

        session.BuyingQueues.Select(q => q.BuySellId).Should().Equal(11, 13);
        var queued = session.BuyingQueues.Single(q => q.BuySellId == 13);
        queued.BuyerUserId.Should().Be(Viewer);
        queued.SellerUserId.Should().BeNull();
        queued.QueueStatus.Should().Be("In Queue (2)");
    }

    [Fact]
    public async Task GetDashboardAsync_PendingPayments_OnlyViewersUnsettledCompletedTransactions()
    {
        // Arrange: past and future sessions alike, since old debts never appear in the session list
        await SeedSessionsAsync(NewSession(1, -400, note: "cancelled"), NewSession(2, 3));
        await SeedBuySellsAsync(
            NewBuySell(1, 1, Viewer, "seller"),                              // unpaid buy
            NewBuySell(2, 2, Viewer, "seller", paymentSent: true),           // paid buy
            NewBuySell(3, 2, Viewer, null),                                  // standing buy request
            NewBuySell(4, 2, "buyer", Viewer, paymentSent: true),            // unconfirmed sell
            NewBuySell(5, 1, "buyer", Viewer, paymentReceived: true),        // confirmed sell
            NewBuySell(6, 2, null, Viewer),                                  // spot still listed
            NewBuySell(7, 2, Other, "seller"));                              // somebody else's

        // Act
        var result = await GetDashboardAsync();

        // Assert
        result.PendingPayments.Select(p => p.BuySellId).Should().Equal(1, 4);
        result.PendingPayments.First().Seller!.FirstName.Should().Be("seller-first");
        result.PendingPayments.Last().Buyer!.LastName.Should().Be("buyer-last");
    }

    [Fact]
    public async Task GetDashboardAsync_GoalieStartsByYear_CountsViewersPastLiveStartsByYear()
    {
        // Arrange
        var lastYear = _now.Year - 1;
        var twoYearsAgo = _now.Year - 2;
        var daysTo = (int year) => (new DateTime(year, 6, 15, 7, 30, 0) - _now).TotalDays;
        await SeedSessionsAsync(
            NewSession(1, daysTo(lastYear)),
            NewSession(2, daysTo(lastYear) + 7, note: null),
            NewSession(3, daysTo(twoYearsAgo)),
            NewSession(4, daysTo(lastYear) + 14, note: "Cancelled"),
            NewSession(5, daysTo(lastYear) + 21),
            NewSession(6, daysTo(lastYear) + 28),
            NewSession(7, 5));
        var joined = _now.AddYears(-3);
        await SeedRosterAsync(
            RosterRow(1, Viewer, PositionPreference.Goalie, true, joined),
            RosterRow(2, Viewer, PositionPreference.Goalie, true, joined),
            RosterRow(3, Viewer, PositionPreference.Goalie, true, joined),
            RosterRow(4, Viewer, PositionPreference.Goalie, true, joined),   // cancelled
            RosterRow(5, Viewer, PositionPreference.Goalie, false, joined),  // benched
            RosterRow(6, Viewer, PositionPreference.Forward, true, joined),  // skated
            RosterRow(6, Other, PositionPreference.Goalie, true, joined),    // someone else in net
            RosterRow(7, Viewer, PositionPreference.Goalie, true, joined));  // not played yet

        // Act
        var result = await GetDashboardAsync();

        // Assert
        result.GoalieStartsByYear.Select(g => (g.Year, g.Starts)).Should().Equal((twoYearsAgo, 1), (lastYear, 2));
    }
}
