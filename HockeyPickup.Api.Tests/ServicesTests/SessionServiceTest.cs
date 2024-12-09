using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using HockeyPickup.Api.Tests.DataRepositoryTests;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HockeyPickup.Api.Tests.Services;

public partial class SessionServiceTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _sessionRepository;
    private readonly Mock<IServiceBus> _serviceBus;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ILogger<UserService>> _logger;
    private readonly SessionService _sessionService;

    public SessionServiceTests()
    {
        // Setup UserManager mock with proper dependencies
        var userStore = new Mock<IUserStore<AspNetUser>>();
        _userManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<AspNetUser>>(),
            Array.Empty<IUserValidator<AspNetUser>>(),
            Array.Empty<IPasswordValidator<AspNetUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<AspNetUser>>>());

        _sessionRepository = new Mock<ISessionRepository>();
        _serviceBus = new Mock<IServiceBus>();
        _configuration = new Mock<IConfiguration>();
        _logger = new Mock<ILogger<UserService>>();

        _sessionService = new SessionService(
            _userManager.Object,
            _sessionRepository.Object,
            _serviceBus.Object,
            _configuration.Object,
            _logger.Object);
    }

    private static SessionDetailedResponse CreateTestSession(string userId, int position)
    {
        return new SessionDetailedResponse
        {
            SessionId = 1,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.Date,
            Note = string.Empty,
            CurrentRosters = new List<Models.Responses.RosterPlayer>
            {
                new()
                {
                    SessionRosterId = 1,
                    UserId = userId,
                    FirstName = "Test",
                    LastName = "User",
                    SessionId = 1,
                    TeamAssignment = 1,
                    IsPlaying = true,
                    IsRegular = false,
                    PlayerStatus = PlayerStatus.Substitute,
                    Rating = 1.0m,
                    Preferred = false,
                    PreferredPlus = false,
                    LastBuySellId = null,
                    JoinedDateTime = DateTime.UtcNow,
                    Position = position,
                    CurrentPosition = "Defense"
                }
            }
        };
    }
}

public partial class SessionServiceTests
{
    [Fact]
    public async Task UpdateRosterPosition_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newPosition = 1;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = CreateTestSession(userId, 2);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _sessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));
        _sessionRepository.Setup(x => x.UpdatePlayerPositionAsync(sessionId, userId, newPosition))
            .Returns(Task.FromResult(session));
        _sessionRepository.Setup(x => x.AddActivityAsync(sessionId, It.IsAny<string>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, newPosition);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        _sessionRepository.Verify(x => x.UpdatePlayerPositionAsync(sessionId, userId, newPosition), Times.Once);
        _sessionRepository.Verify(x => x.AddActivityAsync(sessionId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRosterPosition_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .Returns(Task.FromResult<AspNetUser?>(null));

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, "nonexistent", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Roster player not found", result.Message);
        _sessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "testUser" };
        _userManager.Setup(x => x.FindByIdAsync("testUser"))
            .Returns(Task.FromResult(user)!);

        // Instead of throwing KeyNotFoundException, we'll have the repository return null
        _sessionRepository.Setup(x => x.GetSessionAsync(It.IsAny<int>()))
            .Returns(Task.FromResult<SessionDetailedResponse>(null!));

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, "testUser", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);
        _sessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_SamePosition_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var currentPosition = 1;
        var user = new AspNetUser { Id = userId };
        var session = CreateTestSession(userId, currentPosition);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _sessionRepository.Setup(x => x.GetSessionAsync(It.IsAny<int>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, userId, currentPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("New position is the same as the current position", result.Message);
        _sessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, "testUser", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Test exception", result.Message);

        // Correct way to verify ILogger
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void ParsePositionName_InvalidPosition_ReturnsEmpty(int position)
    {
        // Act
        var result = position.ParsePositionName();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "TBD")]
    [InlineData(1, "Forward")]
    [InlineData(2, "Defense")]
    public void ParsePositionName_ValidPosition_ReturnsCorrectName(int position, string expected)
    {
        // Act
        var result = position.ParsePositionName();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task SessionRoster_NavigationProperties_LoadCorrectly()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<HockeyPickupContext>()
            .UseSqlite(connection)
            .Options;

        // Create the schema and seed data
        await using var context = new DetailedSessionTestContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new AspNetUser
        {
            Id = "testUser",
            UserName = "test@example.com",
            Email = "test@example.com",
            PayPalEmail = "test@example.com",
            NotificationPreference = 1
        };
        context.Users!.Add(user);

        var session = new Session
        {
            SessionId = 1,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.AddDays(1)
        };
        context.Sessions!.Add(session);
        await context.SaveChangesAsync();

        var roster = new SessionRoster
        {
            SessionRosterId = 1,
            SessionId = 1,
            UserId = "testUser",
            Position = 1,
            TeamAssignment = 2,
            IsPlaying = true,
            IsRegular = false,
            JoinedDateTime = DateTime.UtcNow,
            LeftDateTime = DateTime.UtcNow.AddHours(1),
            LastBuySellId = 123
        };
        context.SessionRosters!.Add(roster);
        await context.SaveChangesAsync();

        // Act & Assert
        var loadedRoster = await context.SessionRosters
            .Include(r => r.Session)
            .Include(r => r.User)
            .FirstAsync();

        loadedRoster.Session.Should().NotBeNull();
        loadedRoster.User.Should().NotBeNull();
        loadedRoster.SessionId.Should().Be(1);
        loadedRoster.UserId.Should().Be("testUser");
        loadedRoster.Position.Should().Be(1);
        loadedRoster.TeamAssignment.Should().Be(2);
        loadedRoster.IsPlaying.Should().BeTrue();
        loadedRoster.IsRegular.Should().BeFalse();
        loadedRoster.JoinedDateTime.Should().NotBe(default);
        loadedRoster.LeftDateTime.Should().NotBeNull();
        loadedRoster.LastBuySellId.Should().Be(123);
    }
}
