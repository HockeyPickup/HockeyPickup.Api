using FluentAssertions;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using HockeyPickup.Api.Tests.DataRepositoryTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class SessionServiceTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IServiceBus> _serviceBus;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly Mock<ISubscriptionHandler> _mockSubscriptionHandler;
    private readonly SessionService _sessionService;
    private readonly Mock<IHttpContextAccessor> _mockContextAccessor;
    private readonly Mock<IUserRepository> _mockUserRepository;

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
        _mockSubscriptionHandler = new Mock<ISubscriptionHandler>();
        _mockContextAccessor = new Mock<IHttpContextAccessor>();
        _mockUserRepository = new Mock<IUserRepository>();

        var mockHttpContext = new Mock<HttpContext>();
        var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "testUserId")
        }));
        mockHttpContext.Setup(x => x.User).Returns(mockUser);
        _mockContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        _sessionService = new SessionService(
            _userManager.Object,
            _mockSessionRepository.Object,
            _serviceBus.Object,
            _configuration.Object,
            _mockLogger.Object,
            _mockSubscriptionHandler.Object,
            _mockContextAccessor.Object,
            _mockUserRepository.Object);
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
                    TeamAssignment = (TeamAssignment) team,
                    IsPlaying = true,
                    IsRegular = false,
                    PlayerStatus = PlayerStatus.Substitute,
                    Rating = 1.0m,
                    Preferred = false,
                    PreferredPlus = false,
                    LastBuySellId = null,
                    JoinedDateTime = DateTime.UtcNow,
                    Position = (PositionPreference) position,
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
        _mockSessionRepository.Setup(x => x.UpdatePlayerPositionAsync(sessionId, userId, (PositionPreference) newPosition))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.AddActivityAsync(sessionId, It.IsAny<string>()))
            .Returns(Task.FromResult(session));

        // Act
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, (PositionPreference) newPosition);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(sessionId, userId, (PositionPreference) newPosition), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(sessionId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRosterPosition_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .Returns(Task.FromResult<AspNetUser?>(null));

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, "nonexistent", (PositionPreference) 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>()), Times.Never);
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
        var result = await _sessionService.UpdateRosterPosition(1, "testUser", (PositionPreference) 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>()), Times.Never);
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
        var result = await _sessionService.UpdateRosterPosition(1, userId, (PositionPreference) currentPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("New position is the same as the current position", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, "testUser", (PositionPreference) 1);

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
    [InlineData(4)]
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
    [InlineData(3, "Goalie")]
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
            NotificationPreference = (NotificationPreference) 1,
            PositionPreference = (PositionPreference) 1
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
            Position = (PositionPreference) 1,
            TeamAssignment = (TeamAssignment) 2,
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
        loadedRoster.Position.Should().Be((PositionPreference) 1);
        loadedRoster.TeamAssignment.Should().Be((TeamAssignment) 2);
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
    public async Task UpdateRosterTeam_Success_SendsServiceBusMessage()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newTeam = 2;
        var currentTeam = 1;
        var baseUrl = "https://test.com";
        var sessionUrl = $"{baseUrl}/session/{sessionId}";

        var user = new AspNetUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };

        var session = CreateTestSession(userId, 2, currentTeam);

        var mockUsers = new List<UserDetailedResponse>
        {
            new UserDetailedResponse
            {
                Id = "user1",
                Email = "user1@test.com",
                Active = true,
                NotificationPreference = (NotificationPreference)1,
                UserName = "user1",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m
            }
        };

        // Setup configuration
        var queueConfigured = false;
        _configuration.Setup(x => x["ServiceBusCommsQueueName"])
            .Callback(() => queueConfigured = true)
            .Returns("testqueue");
        _configuration.Setup(x => x["BaseUrl"])
            .Returns(baseUrl);

        // Setup repository calls with debug checks
        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));

        _mockSessionRepository.Setup(x => x.UpdatePlayerTeamAsync(sessionId, userId, (TeamAssignment) newTeam))
            .Returns(Task.FromResult(session));

        _mockSessionRepository.Setup(x => x.AddActivityAsync(sessionId, It.IsAny<string>()))
            .Returns(Task.FromResult(session));

        var usersReturned = false;
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .Callback(() => usersReturned = true)
            .ReturnsAsync(mockUsers);

        _mockSubscriptionHandler.Setup(x => x.HandleUpdate(It.IsAny<SessionDetailedResponse>()))
            .Returns(Task.CompletedTask);

        // Track ServiceBus call
        var serviceBusMessageCalled = false;
        ServiceBusCommsMessage? capturedMessage = null;

        _serviceBus
            .Setup(x => x.SendAsync(
                It.IsAny<ServiceBusCommsMessage>(),
                It.Is<string>(s => s == "TeamAssignmentChange"),
                It.IsAny<string>(),
                It.Is<string>(s => s == "testqueue"),
                default))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken>((msg, subject, corrId, queue, token) =>
            {
                serviceBusMessageCalled = true;
                capturedMessage = msg;
            })
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(usersReturned, "User repository was not called");
        Assert.True(queueConfigured, "Queue configuration was not accessed");
        Assert.True(serviceBusMessageCalled, "Service bus message was not sent");

        // Verify message content
        if (capturedMessage != null)
        {
            Assert.Equal("TeamAssignmentChange", capturedMessage.Metadata["Type"]);
            Assert.Equal(userId, capturedMessage.RelatedEntities["UserId"]);
            Assert.Equal("Test", capturedMessage.RelatedEntities["FirstName"]);
            Assert.Equal("User", capturedMessage.RelatedEntities["LastName"]);
            Assert.Equal(sessionUrl, capturedMessage.MessageData["SessionUrl"]);
            Assert.Equal("Light", capturedMessage.MessageData["FormerTeamAssignment"]);
            Assert.Equal("Dark", capturedMessage.MessageData["NewTeamAssignment"]);
            Assert.Contains("user1@test.com", capturedMessage.NotificationEmails);
        }

        _mockSubscriptionHandler.Verify(x => x.HandleUpdate(It.IsAny<SessionDetailedResponse>()), Times.Once);
    }
}

public partial class SessionServiceTests
{
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
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, (PositionPreference) newPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>()), Times.Never);
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
        var result = await _sessionService.UpdateRosterPosition(sessionId, userId, (PositionPreference) newPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>()), Times.Never);
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
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
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
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User is not part of this session's current roster", result.Message);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
    }

    [Fact]
    public async Task CreateSession_Success_SendsServiceBusMessage()
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

        var mockUsers = new List<UserDetailedResponse>
        {
            new()
            {
                Id = "user1",
                Email = "user1@test.com",
                Active = true,
                NotificationPreference = (NotificationPreference)1,
                FirstName = "Test",
                LastName = "User",
                UserName = "testuser",
                Rating = 1.0m,
                Preferred = false,
                PreferredPlus = false
            }
        };

        var testUser = new AspNetUser
        {
            Id = "testUserId",
            FirstName = "Test",
            LastName = "User"
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

        var queueConfigured = false;
        _configuration.Setup(x => x["ServiceBusCommsQueueName"])
            .Callback(() => queueConfigured = true)
            .Returns("testqueue");
        _configuration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");

        // Setup mocks with debug checks
        _mockSessionRepository.Setup(x => x.CreateSessionAsync(It.IsAny<Session>()))
            .Callback<Session>(session =>
            {
                Assert.Equal(request.SessionDate, session.SessionDate);
            })
            .ReturnsAsync(createdSession);

        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(createdSession);

        var usersReturned = false;
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .Callback(() => usersReturned = true)
            .ReturnsAsync(mockUsers);

        var mockHttpContext = new DefaultHttpContext();
        mockHttpContext.Items["UserId"] = "testUserId";
        _mockContextAccessor.Object.HttpContext = mockHttpContext;

        var userFound = false;
        _userManager.Setup(x => x.FindByIdAsync("testUserId"))
            .Callback<string>(id => userFound = true)
            .ReturnsAsync(testUser);

        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(x => x.Value)
            .Callback(() => queueConfigured = true)
            .Returns("testqueue");
        _configuration.Setup(x => x.GetSection("ServiceBusCommsQueueName"))
            .Returns(mockConfigSection.Object);

        var serviceBusMessageCalled = false;
        ServiceBusCommsMessage? capturedMessage = null;

        _serviceBus
                .Setup(x => x.SendAsync(
                    It.IsAny<ServiceBusCommsMessage>(),
                    It.Is<string>(s => s == "CreateSession"),
                    It.IsAny<string>(),
                    It.Is<string>(s => s == "testqueue"),
                    default))
                .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken>((msg, subject, corrId, queue, token) =>
                {
                    serviceBusMessageCalled = true;
                    capturedMessage = msg;
                    Console.WriteLine($"Service bus called with queue: {queue}");
                })
                .Returns(Task.FromResult(true));

        // Act
        var result = await _sessionService.CreateSession(request);

        // Debug Assertions
        Assert.True(userFound, "User manager was not called");
        Assert.True(usersReturned, "User repository was not called");
        Assert.True(queueConfigured, "Queue configuration was not accessed");
        Assert.NotNull(result.Data);

        // Main Assertions
        Assert.True(result.IsSuccess);
        Assert.True(serviceBusMessageCalled, "Service bus message was not sent");

        if (capturedMessage != null)
        {
            Assert.Equal("CreateSession", capturedMessage.Metadata["Type"]);
            Assert.Equal("Test User", capturedMessage.MessageData["CreatedByName"]);
            Assert.Equal("user1@test.com", capturedMessage.NotificationEmails.FirstOrDefault());
        }
    }
}

public partial class SessionServiceTests
{
    [Fact]
    public async Task DeleteSessionAsync_Success_ReturnsTrue()
    {
        // Arrange
        var sessionId = 1;
        var existingSession = new SessionDetailedResponse
        {
            SessionId = sessionId,
            SessionDate = DateTime.UtcNow,
            RegularSetId = 1,
            CreateDateTime = DateTime.UtcNow.AddDays(-1),
            UpdateDateTime = DateTime.UtcNow.AddDays(-1)
        };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync(existingSession);
        _mockSessionRepository.Setup(x => x.DeleteSessionAsync(sessionId))
            .ReturnsAsync(true);
        _mockSubscriptionHandler.Setup(x => x.HandleDelete(sessionId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sessionService.DeleteSessionAsync(sessionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal($"Deleted Session {sessionId}", result.Message);
        _mockSessionRepository.Verify(x => x.DeleteSessionAsync(sessionId), Times.Once);
        _mockSubscriptionHandler.Verify(x => x.HandleDelete(sessionId), Times.Once);
    }

    [Fact]
    public async Task DeleteSessionAsync_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync((SessionDetailedResponse) null!);

        // Act
        var result = await _sessionService.DeleteSessionAsync(sessionId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);
        _mockSessionRepository.Verify(x => x.DeleteSessionAsync(sessionId), Times.Never);
        _mockSubscriptionHandler.Verify(x => x.HandleDelete(sessionId), Times.Never);
    }

    [Fact]
    public async Task DeleteSessionAsync_DeleteFails_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var existingSession = new SessionDetailedResponse
        {
            SessionId = sessionId,
            SessionDate = DateTime.UtcNow,
            RegularSetId = 1,
            CreateDateTime = DateTime.UtcNow.AddDays(-1),
            UpdateDateTime = DateTime.UtcNow.AddDays(-1)
        };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync(existingSession);
        _mockSessionRepository.Setup(x => x.DeleteSessionAsync(sessionId))
            .ReturnsAsync(false);

        // Act
        var result = await _sessionService.DeleteSessionAsync(sessionId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to delete session", result.Message);
        _mockSessionRepository.Verify(x => x.DeleteSessionAsync(sessionId), Times.Once);
        _mockSubscriptionHandler.Verify(x => x.HandleDelete(sessionId), Times.Never);
    }

    [Fact]
    public async Task DeleteSessionAsync_ThrowsException_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var expectedException = new Exception("Test exception");

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _sessionService.DeleteSessionAsync(sessionId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal($"An error occurred deleting the session: {expectedException.Message}", result.Message);
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task DeleteSessionAsync_SubscriptionHandlerThrows_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var expectedException = new Exception("Subscription handler error");
        var existingSession = new SessionDetailedResponse
        {
            SessionId = sessionId,
            SessionDate = DateTime.UtcNow,
            RegularSetId = 1,
            CreateDateTime = DateTime.UtcNow.AddDays(-1),
            UpdateDateTime = DateTime.UtcNow.AddDays(-1)
        };

        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync(existingSession);
        _mockSessionRepository.Setup(x => x.DeleteSessionAsync(sessionId))
            .ReturnsAsync(true);
        _mockSubscriptionHandler.Setup(x => x.HandleDelete(sessionId))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _sessionService.DeleteSessionAsync(sessionId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal($"An error occurred deleting the session: {expectedException.Message}", result.Message);
        _mockSessionRepository.Verify(x => x.DeleteSessionAsync(sessionId), Times.Once);
        _mockSubscriptionHandler.Verify(x => x.HandleDelete(sessionId), Times.Once);
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRosterTeam_Success_FiltersDifferentNotificationPreferences()
    {
        // Arrange
        var userId = "testUser";
        var sessionId = 1;
        var newTeam = 2;
        var user = new AspNetUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };
        var session = CreateTestSession(userId, 2, 1);

        var mockUsers = new List<UserDetailedResponse>
        {
            new UserDetailedResponse
            {
                Id = "user1",
                Email = "active.all@test.com",
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "user1",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m
            },
            new UserDetailedResponse
            {
                Id = "user2",
                Email = "active.none@test.com",
                Active = true,
                NotificationPreference = NotificationPreference.None,
                UserName = "user2",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m
            },
            new UserDetailedResponse
            {
                Id = "user3",
                Email = "inactive.all@test.com",
                Active = false,
                NotificationPreference = NotificationPreference.All,
                UserName = "user3",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m
            },
            new UserDetailedResponse
            {
                Id = "user4",
                Email = "", // Empty email
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "user4",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m
            },
            new UserDetailedResponse
            {
                Id = "user5",
                Email = null, // Null email
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "user5",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .Returns(Task.FromResult(user)!);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.UpdatePlayerTeamAsync(sessionId, userId, (TeamAssignment) newTeam))
            .Returns(Task.FromResult(session));
        _mockSessionRepository.Setup(x => x.AddActivityAsync(sessionId, It.IsAny<string>()))
            .Returns(Task.FromResult(session));
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(mockUsers);
        _configuration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _configuration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");

        ServiceBusCommsMessage? capturedMessage = null;
        _serviceBus
            .Setup(x => x.SendAsync(
                It.IsAny<ServiceBusCommsMessage>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                default))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken>((msg, _, _, _, _) =>
            {
                capturedMessage = msg;
            })
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedMessage);

        // Verify only active users with All preference and valid emails are included
        Assert.Contains("active.all@test.com", capturedMessage!.NotificationEmails);
        Assert.DoesNotContain("active.none@test.com", capturedMessage.NotificationEmails);
        Assert.DoesNotContain("inactive.all@test.com", capturedMessage.NotificationEmails);
        Assert.Single(capturedMessage.NotificationEmails); // Only one valid email should be included
    }

    [Fact]
    public async Task CreateSession_Success_FiltersDifferentNotificationPreferences()
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

        var mockUsers = new List<UserDetailedResponse>
        {
            new UserDetailedResponse
            {
                Id = "user1",
                Email = "active.all@test.com",
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "user1",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m,
                FirstName = "Active",
                LastName = "All"
            },
            new UserDetailedResponse
            {
                Id = "user2",
                Email = "active.none@test.com",
                Active = true,
                NotificationPreference = NotificationPreference.None,
                UserName = "user2",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m,
                FirstName = "Active",
                LastName = "None"
            },
            new UserDetailedResponse
            {
                Id = "user3",
                Email = "inactive.all@test.com",
                Active = false,
                NotificationPreference = NotificationPreference.All,
                UserName = "user3",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m,
                FirstName = "Inactive",
                LastName = "All"
            },
            new UserDetailedResponse
            {
                Id = "user4",
                Email = "", // Empty email
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "user4",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m,
                FirstName = "Empty",
                LastName = "Email"
            },
            new UserDetailedResponse
            {
                Id = "user5",
                Email = null, // Null email
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "user5",
                Preferred = false,
                PreferredPlus = false,
                Rating = 1.0m,
                FirstName = "Null",
                LastName = "Email"
            }
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

        var testUser = new AspNetUser
        {
            Id = "testUserId",
            FirstName = "Test",
            LastName = "User"
        };

        _mockSessionRepository.Setup(x => x.CreateSessionAsync(It.IsAny<Session>()))
            .ReturnsAsync(createdSession);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(createdSession);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(mockUsers);
        _userManager.Setup(x => x.FindByIdAsync("testUserId"))
            .ReturnsAsync(testUser);
        _configuration.Setup(x => x["BaseUrl"])
            .Returns("https://test.com");
        _configuration.Setup(x => x["ServiceBusCommsQueueName"])
            .Returns("testqueue");

        var mockHttpContext = new DefaultHttpContext();
        mockHttpContext.Items["UserId"] = "testUserId";
        _mockContextAccessor.Object.HttpContext = mockHttpContext;

        ServiceBusCommsMessage? capturedMessage = null;
        _serviceBus
            .Setup(x => x.SendAsync(
                It.IsAny<ServiceBusCommsMessage>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                default))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken>((msg, _, _, _, _) =>
            {
                capturedMessage = msg;
            })
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sessionService.CreateSession(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedMessage);

        // Verify only active users with All preference and valid emails are included
        Assert.Contains("active.all@test.com", capturedMessage!.NotificationEmails);
        Assert.DoesNotContain("active.none@test.com", capturedMessage.NotificationEmails);
        Assert.DoesNotContain("inactive.all@test.com", capturedMessage.NotificationEmails);
        Assert.Single(capturedMessage.NotificationEmails); // Only one valid email should be included
    }

    [Fact]
    public async Task UpdateRosterTeam_UserNull_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var userId = "nonexistentUser";
        var newTeam = 2;

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser?) null);

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Message);

        // Verify repository methods were not called
        _mockSessionRepository.Verify(x => x.GetSessionAsync(It.IsAny<int>()), Times.Never);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _serviceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_SessionNull_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var userId = "testUser";
        var newTeam = 2;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync((SessionDetailedResponse?) null!);

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found", result.Message);

        // Verify subsequent methods were not called
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _serviceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_SameTeamAssignment_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var userId = "testUser";
        var currentTeam = 2;
        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var session = CreateTestSession(userId, 1, currentTeam); // Position 1, Team 2

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(sessionId))
            .ReturnsAsync(session);

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) currentTeam); // Try to update to same team

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("New team assignment is the same as the current team assignment", result.Message);

        // Verify subsequent methods were not called
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _serviceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_ThrowsException_ReturnsFailure()
    {
        // Arrange
        var sessionId = 1;
        var userId = "testUser";
        var newTeam = 2;
        var expectedException = new Exception("Test exception");

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _sessionService.UpdateRosterTeam(sessionId, userId, (TeamAssignment) newTeam);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal($"An error occurred updating player team assignment: {expectedException.Message}", result.Message);

        // Verify logger was called
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        // Verify no other methods were called
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _serviceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }
}
