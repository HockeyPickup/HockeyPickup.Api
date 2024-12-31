using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
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

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class SessionServiceTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IServiceBus> _serviceBus;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ILogger<UserService>> _mockLogger;
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

        _mockSessionRepository = new Mock<ISessionRepository>();
        _serviceBus = new Mock<IServiceBus>();
        _configuration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<UserService>>();

        _sessionService = new SessionService(
            _userManager.Object,
            _mockSessionRepository.Object,
            _serviceBus.Object,
            _configuration.Object,
            _mockLogger.Object);
    }

    private static SessionDetailedResponse CreateTestSession(string userId, int position, int team)
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
                    Email = "user@anywhere.com",
                    SessionId = 1,
                    TeamAssignment = team,
                    IsPlaying = true,
                    IsRegular = false,
                    PlayerStatus = PlayerStatus.Substitute,
                    Rating = 1.0m,
                    Preferred = false,
                    PreferredPlus = false,
                    LastBuySellId = null,
                    JoinedDateTime = DateTime.UtcNow,
                    Position = position,
                    CurrentPosition = "Defense",
                    PhotoUrl = "https://example.com/photo.jpg"
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
        var session = CreateTestSession(userId, 2, 1);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.UpdatePlayerPositionAsync(sessionId, userId, newPosition))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.AddActivityAsync(sessionId, It.IsAny<string>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, newPosition);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(sessionId, userId, newPosition), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(sessionId, It.IsAny<string>()), Times.Once);
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
        Assert.Equal("User not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "testUser" };
        _userManager.Setup(x => x.FindByIdAsync("testUser"))
            .Returns(Task.FromResult(user)!);

        // Instead of throwing KeyNotFoundException, we'll have the repository return null
        _mockSessionRepository.Setup(x => x.GetSessionAsync(It.IsAny<int>()))
            .Returns(Task.FromResult<SessionDetailedResponse>(null!));

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, "testUser", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_SamePosition_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var currentPosition = 1;
        var user = new AspNetUser { Id = userId };
        var session = CreateTestSession(userId, currentPosition, 1);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(It.IsAny<int>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, userId, currentPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("New position is the same as the current position", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
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
        _mockLogger.Verify(
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

public partial class SessionServiceTests
{
    [Fact]
    public async Task UpdateRosterTeam_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newTeam = 2;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = CreateTestSession(userId, 2, 1);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.UpdatePlayerTeamAsync(sessionId, userId, newTeam))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.AddActivityAsync(sessionId, It.IsAny<string>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, newTeam);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(sessionId, userId, newTeam), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(sessionId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRosterTeam_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .Returns(Task.FromResult<AspNetUser?>(null));

        // Act
        var result = await _sessionService.UpdateRosterTeam(1, "nonexistent", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "testUser" };
        _userManager.Setup(x => x.FindByIdAsync("testUser"))
            .Returns(Task.FromResult(user)!);

        // Instead of throwing KeyNotFoundException, we'll have the repository return null
        _mockSessionRepository.Setup(x => x.GetSessionAsync(It.IsAny<int>()))
            .Returns(Task.FromResult<SessionDetailedResponse>(null!));

        // Act
        var result = await _sessionService.UpdateRosterTeam(1, "testUser", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_SameTeam_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var currentTeam = 1;
        var user = new AspNetUser { Id = userId };
        var session = CreateTestSession(userId, 1, currentTeam);

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(It.IsAny<int>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterTeam(1, userId, currentTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("New team assignment is the same as the current team assignment", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _sessionService.UpdateRosterTeam(1, "testUser", 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Test exception", result.Message);

        // Correct way to verify ILogger
        _mockLogger.Verify(
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
    public void ParseTeamName_InvalidTeam_ReturnsEmpty(int team)
    {
        // Act
        var result = team.ParseTeamName();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "TBD")]
    [InlineData(1, "Light")]
    [InlineData(2, "Dark")]
    public void ParseTeamName_ValidTeam_ReturnsCorrectName(int team, string expected)
    {
        // Act
        var result = team.ParseTeamName();

        // Assert
        result.Should().Be(expected);
    }
}

public partial class SessionServiceTests
{
    [Fact]
    public async Task CreateSession_Success_ReturnsSessionResponse()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Test session"
        };

        var createdSession = new SessionDetailedResponse
        {
            SessionId = 1,
            SessionDate = request.SessionDate,
            RegularSetId = request.RegularSetId,
            BuyDayMinimum = request.BuyDayMinimum,
            Cost = request.Cost,
            Note = request.Note,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(x => x.CreateSessionAsync(It.IsAny<Session>()))
            .ReturnsAsync(createdSession);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(createdSession);

        // Act
        var result = await _sessionService.CreateSession(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(createdSession.SessionId, result.Data.SessionId);
        _mockSessionRepository.Verify(x => x.CreateSessionAsync(It.IsAny<Session>()), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSession_Success_ReturnsUpdatedSession()
    {
        // Arrange
        var request = new UpdateSessionRequest
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Updated session"
        };

        var existingSession = new SessionDetailedResponse
        {
            SessionId = request.SessionId,
            SessionDate = DateTime.UtcNow,
            RegularSetId = 2,
            BuyDayMinimum = 2,
            Cost = 25.00m,
            Note = "Original session",
            CreateDateTime = DateTime.UtcNow.AddDays(-1),
            UpdateDateTime = DateTime.UtcNow.AddDays(-1)
        };

        var updatedSession = new SessionDetailedResponse
        {
            SessionId = request.SessionId,
            SessionDate = request.SessionDate,
            RegularSetId = request.RegularSetId,
            BuyDayMinimum = request.BuyDayMinimum,
            Cost = request.Cost,
            Note = request.Note,
            CreateDateTime = existingSession.CreateDateTime,
            UpdateDateTime = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(existingSession);
        _mockSessionRepository.Setup(x => x.UpdateSessionAsync(It.IsAny<Session>()))
            .ReturnsAsync(updatedSession);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(updatedSession);

        // Act
        var result = await _sessionService.UpdateSession(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(updatedSession.SessionId, result.Data.SessionId);
        Assert.Equal(request.SessionDate, result.Data.SessionDate);
        Assert.Equal(request.RegularSetId, result.Data.RegularSetId);
        Assert.Equal(request.BuyDayMinimum, result.Data.BuyDayMinimum);
        Assert.Equal(request.Cost, result.Data.Cost);
        Assert.Equal(request.Note, result.Data.Note);
        _mockSessionRepository.Verify(x => x.UpdateSessionAsync(It.IsAny<Session>()), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSession_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new UpdateSessionRequest
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Updated session"
        };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .Returns(Task.FromResult<SessionDetailedResponse>(null!));

        // Act
        var result = await _sessionService.UpdateSession(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdateSessionAsync(It.IsAny<Session>()), Times.Never);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateSession_ThrowsException_ReturnsFailure()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Test session"
        };

        _mockSessionRepository.Setup(x => x.CreateSessionAsync(It.IsAny<Session>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _sessionService.CreateSession(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("An error occurred creating the session: Test exception", result.Message);
        // Logger verification needs to use It.IsAny<T> for all parameters
        _mockLogger.Verify(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ));
    }

    [Fact]
    public async Task UpdateSession_ThrowsException_ReturnsFailure()
    {
        // Arrange 
        var request = new UpdateSessionRequest
        {
            SessionId = 1,
            SessionDate = DateTime.UtcNow.AddDays(1),
            RegularSetId = 1,
            BuyDayMinimum = 1,
            Cost = 20.00m,
            Note = "Test session"
        };

        var existingSession = new SessionDetailedResponse
        {
            SessionId = request.SessionId,
            SessionDate = DateTime.UtcNow,
            RegularSetId = 2,
            BuyDayMinimum = 2,
            Cost = 25.00m,
            Note = "Original session",
            CreateDateTime = DateTime.UtcNow.AddDays(-1),
            UpdateDateTime = DateTime.UtcNow.AddDays(-1)
        };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(request.SessionId))
            .ReturnsAsync(existingSession);

        _mockSessionRepository.Setup(x => x.UpdateSessionAsync(It.IsAny<Session>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _sessionService.UpdateSession(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("An error occurred updating the session: Test exception", result.Message);
        _mockLogger.Verify(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ));
    }

    [Fact]
    public async Task UpdateRosterPosition_UserNotInSession_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newPosition = 1;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = CreateTestSession("differentUser", 2, 1); // Note: Different user

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, newPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_EmptyRoster_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newPosition = 1;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = new SessionDetailedResponse
        {
            SessionId = sessionId,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.Date,
            Note = string.Empty,
            CurrentRosters = new List<Models.Responses.RosterPlayer>() // Empty roster
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, newPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_UserNotInSession_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newTeam = 1;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = CreateTestSession("differentUser", 1, 2); // Note: Different user

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_EmptyRoster_ReturnsFailure()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newTeam = 1;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = new SessionDetailedResponse
        {
            SessionId = sessionId,
            CreateDateTime = DateTime.UtcNow,
            UpdateDateTime = DateTime.UtcNow,
            SessionDate = DateTime.UtcNow.Date,
            Note = string.Empty,
            CurrentRosters = new List<Models.Responses.RosterPlayer>() // Empty roster
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
