using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.DataRepositoryTests;

public class SessionRepositoryGoalieTests : IDisposable
{
    private readonly Mock<ILogger<SessionRepository>> _mockLogger;
    private readonly Mock<HttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<HockeyPickupContext> _options;
    private readonly DateTime _testDate = DateTime.UtcNow;

    public SessionRepositoryGoalieTests()
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
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private SessionRepository CreateRepository(HockeyPickupContext context) =>
        new(context, _mockLogger.Object, _mockContextAccessor.Object, _mockConfiguration.Object);

    private async Task SeedSessionAndUsersAsync(params string[] userIds)
    {
        await using var context = new DetailedSessionTestContext(_options);
        foreach (var userId in userIds)
        {
            context.Users!.Add(new AspNetUser
            {
                Id = userId,
                UserName = $"{userId}@example.com",
                Email = $"{userId}@example.com",
                FirstName = "First",
                LastName = userId,
                NotificationPreference = NotificationPreference.All,
                PositionPreference = PositionPreference.Goalie,
                Shoots = ShootPreference.Left
            });
        }

        context.Sessions!.Add(new Session
        {
            SessionId = 1,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(1),
            Note = "8/10"
        });
        await context.SaveChangesAsync();
    }

    private static CurrentSessionRoster RosterRow(int id, string userId, int position, bool isPlaying, DateTime joined, int team = 0) => new()
    {
        SessionRosterId = id,
        SessionId = 1,
        UserId = userId,
        Email = $"{userId}@example.com",
        FirstName = "First",
        LastName = userId,
        TeamAssignment = team,
        IsPlaying = isPlaying,
        IsRegular = false,
        PlayerStatus = isPlaying ? "Substitute" : "Not Playing",
        Rating = 0,
        PhotoUrl = $"https://photos/{userId}.jpg",
        JoinedDateTime = joined,
        Position = position,
        CurrentPosition = position == 3 ? "Goalie" : "Forward"
    };

    private async Task SeedRosterViewAsync(params CurrentSessionRoster[] rows)
    {
        await using var context = new DetailedSessionTestContext(_options);
        context.CurrentSessionRosters!.AddRange(rows);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddRosterPlayerAsync_NewPlayer_InsertsPlayingRowWithoutBuySell()
    {
        // Arrange
        await SeedSessionAndUsersAsync("goalie1");

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var result = await CreateRepository(context).AddRosterPlayerAsync(1, "goalie1", TeamAssignment.TBD, PositionPreference.Goalie);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(1);
        var row = await context.SessionRosters!.SingleAsync(r => r.SessionId == 1 && r.UserId == "goalie1");
        row.Position.Should().Be(PositionPreference.Goalie);
        row.TeamAssignment.Should().Be(TeamAssignment.TBD);
        row.IsPlaying.Should().BeTrue();
        row.IsRegular.Should().BeFalse();
        row.LastBuySellId.Should().BeNull();
        row.LeftDateTime.Should().BeNull();
        row.JoinedDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AddRosterPlayerAsync_RowAlreadyExists_ThrowsAndLeavesSingleRow()
    {
        // Arrange
        await SeedSessionAndUsersAsync("skater1");
        await using (var seed = new DetailedSessionTestContext(_options))
        {
            seed.SessionRosters!.Add(new SessionRoster
            {
                SessionId = 1,
                UserId = "skater1",
                TeamAssignment = TeamAssignment.Light,
                Position = PositionPreference.Forward,
                IsPlaying = false,
                JoinedDateTime = _testDate
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var act = () => CreateRepository(context).AddRosterPlayerAsync(1, "skater1", TeamAssignment.Dark, PositionPreference.Defense);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Player already has a roster entry for this session");
        context.ChangeTracker.Entries().Should().BeEmpty();
        var rows = await context.SessionRosters!.Where(r => r.SessionId == 1 && r.UserId == "skater1").ToListAsync();
        rows.Should().ContainSingle();
        rows[0].TeamAssignment.Should().Be(TeamAssignment.Light);
        rows[0].IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePlayerPositionAndTeamAsync_ValidPlayer_UpdatesBothInOneSave()
    {
        // Arrange
        await SeedSessionAndUsersAsync("skater1");
        await using (var seed = new DetailedSessionTestContext(_options))
        {
            seed.SessionRosters!.Add(new SessionRoster
            {
                SessionId = 1,
                UserId = "skater1",
                TeamAssignment = TeamAssignment.Dark,
                Position = PositionPreference.Forward,
                IsPlaying = true,
                JoinedDateTime = _testDate
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var result = await CreateRepository(context).UpdatePlayerPositionAndTeamAsync(1, "skater1", PositionPreference.Goalie, TeamAssignment.TBD);

        // Assert
        result.Should().NotBeNull();
        var row = await context.SessionRosters!.SingleAsync(r => r.SessionId == 1 && r.UserId == "skater1");
        row.Position.Should().Be(PositionPreference.Goalie);
        row.TeamAssignment.Should().Be(TeamAssignment.TBD);
    }

    [Fact]
    public async Task UpdatePlayerPositionAndTeamAsync_PlayerNotInRoster_ThrowsKeyNotFoundException()
    {
        // Arrange
        await SeedSessionAndUsersAsync("skater1");

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var act = () => CreateRepository(context).UpdatePlayerPositionAndTeamAsync(1, "skater1", PositionPreference.Goalie, TeamAssignment.TBD);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("Player not found in session roster");
    }

    [Fact]
    public async Task GetSessionAsync_MapsOnlyPlayingGoaliesOrderedByJoined()
    {
        // Arrange
        await SeedSessionAndUsersAsync("late", "early", "benched", "skater");
        await SeedRosterViewAsync(
            RosterRow(1, "late", 3, true, _testDate.AddHours(2)),
            RosterRow(2, "early", 3, true, _testDate.AddHours(1)),
            RosterRow(3, "benched", 3, false, _testDate),
            RosterRow(4, "skater", 1, true, _testDate, team: 1));

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var result = await CreateRepository(context).GetSessionAsync(1);

        // Assert
        result.Goalies.Select(g => g.UserId).Should().Equal("early", "late");
        var first = result.Goalies.First();
        first.FirstName.Should().Be("First");
        first.LastName.Should().Be("early");
        first.PhotoUrl.Should().Be("https://photos/early.jpg");
        first.IsPlaying.Should().BeTrue();
        first.JoinedDateTime.Should().Be(_testDate.AddHours(1));

        // Goalies stay in CurrentRosters too, so everyone-consumers keep working
        result.CurrentRosters.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetSessionAsync_NoGoalies_MapsEmptyList()
    {
        // Arrange
        await SeedSessionAndUsersAsync("skater");
        await SeedRosterViewAsync(RosterRow(1, "skater", 2, true, _testDate, team: 2));

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var result = await CreateRepository(context).GetSessionAsync(1);

        // Assert
        result.Goalies.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetBasicSessionsAsync_ProjectsOnlyPlayingGoaliesOrderedByJoined()
    {
        // Arrange
        await SeedSessionAndUsersAsync("late", "early", "benched", "skater");
        await using (var seed = new DetailedSessionTestContext(_options))
        {
            seed.Sessions!.Add(new Session
            {
                SessionId = 2,
                CreateDateTime = _testDate,
                UpdateDateTime = _testDate,
                SessionDate = _testDate.AddDays(2)
            });
            await seed.SaveChangesAsync();
        }
        await SeedRosterViewAsync(
            RosterRow(1, "late", 3, true, _testDate.AddHours(2)),
            RosterRow(2, "early", 3, true, _testDate.AddHours(1)),
            RosterRow(3, "benched", 3, false, _testDate),
            RosterRow(4, "skater", 1, true, _testDate, team: 1));

        // Act
        await using var context = new DetailedSessionTestContext(_options);
        var result = (await CreateRepository(context).GetBasicSessionsAsync()).ToList();

        // Assert
        result.Select(s => s.SessionId).Should().Equal(2, 1);
        result.Single(s => s.SessionId == 2).Goalies.Should().BeEmpty();
        var goalies = result.Single(s => s.SessionId == 1).Goalies;
        goalies.Select(g => g.UserId).Should().Equal("early", "late");
        goalies.First().PhotoUrl.Should().Be("https://photos/early.jpg");
        goalies.Should().OnlyContain(g => g.IsPlaying);
    }

    [Fact]
    public void GetBasicSessionsQuery_SqlServer_TranslatesToSingleQueryWithRosterJoin()
    {
        // Arrange: the real model against the SQL Server provider; ToQueryString never opens a connection
        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlServer("Server=localhost;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new HockeyPickupContext(options);

        // Act
        var sql = CreateRepository(context).GetBasicSessionsQuery().ToQueryString();

        // Assert: one SELECT batch, goalies pulled in with a LEFT JOIN on the view (no per-session queries)
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(statement => statement.Length > 0 && !statement.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle();
        sql.Should().Contain("LEFT JOIN");
        sql.Should().Contain("[CurrentSessionRoster]");
        sql.Should().Contain("[Position] = 3");
        sql.Should().Contain("[IsPlaying] = CAST(1 AS bit)");
    }
}
