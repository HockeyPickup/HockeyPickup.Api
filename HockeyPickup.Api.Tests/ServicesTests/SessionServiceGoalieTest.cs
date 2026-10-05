using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class SessionServiceTests
{
    private const string AddUserId = "addUser";
    private const int AddSessionId = 1;

    private static AddRosterPlayerRequest AddRequest(PositionPreference position, TeamAssignment team) => new()
    {
        SessionId = AddSessionId,
        UserId = AddUserId,
        Position = position,
        TeamAssignment = team
    };

    private AspNetUser SetupAddUser(bool active = true)
    {
        var user = new AspNetUser
        {
            Id = AddUserId,
            FirstName = "Ryan",
            LastName = "Novak",
            Email = "ryan@example.com",
            Active = active,
            NotificationPreference = NotificationPreference.All
        };
        _userManager.Setup(x => x.FindByIdAsync(AddUserId)).ReturnsAsync(user);
        return user;
    }

    // Wires the success path and captures every Service Bus message sent
    private List<(ServiceBusCommsMessage Message, string Subject)> SetupAddSuccess(SessionDetailedResponse session, SessionDetailedResponse updatedSession)
    {
        _mockSessionRepository.Setup(x => x.GetSessionAsync(AddSessionId)).ReturnsAsync(session);
        _mockSessionRepository.Setup(x => x.AddRosterPlayerAsync(AddSessionId, AddUserId, It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>())).ReturnsAsync(updatedSession);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(AddSessionId, It.IsAny<string>())).ReturnsAsync(updatedSession);
        _mockUserRepository.Setup(x => x.GetDetailedUsersAsync()).ReturnsAsync(new List<UserDetailedResponse>
        {
            new()
            {
                Id = "watcher",
                Email = "watcher@example.com",
                Active = true,
                NotificationPreference = NotificationPreference.All,
                UserName = "watcher",
                Preferred = false,
                PreferredPlus = false,
                Rating = 0m
            }
        });
        _configuration.Setup(x => x["BaseUrl"]).Returns("https://test.com");
        _configuration.Setup(x => x["ServiceBusCommsQueueName"]).Returns("testqueue");

        var sent = new List<(ServiceBusCommsMessage, string)>();
        _serviceBus
            .Setup(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken, DateTimeOffset?>((msg, subject, _, _, _, _) => sent.Add((msg, subject)))
            .Returns(Task.CompletedTask);
        return sent;
    }

    private static SessionDetailedResponse EmptyRosterSession() => new()
    {
        SessionId = AddSessionId,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow,
        SessionDate = new DateTime(2026, 10, 7, 7, 30, 0),
        CurrentRosters = new List<Models.Responses.RosterPlayer>()
    };

    [Fact]
    public async Task AddRosterPlayer_Goalie_IgnoresRequestedTeamStoresTbdAndLogsActivity()
    {
        // Arrange
        SetupAddUser();
        var session = EmptyRosterSession();
        var updated = EmptyRosterSession();
        var sent = SetupAddSuccess(session, updated);

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.Dark));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeSameAs(updated);
        result.Message.Should().Be("Ryan Novak added to roster as Goalie");
        _mockSessionRepository.Verify(x => x.AddRosterPlayerAsync(AddSessionId, AddUserId, TeamAssignment.TBD, PositionPreference.Goalie), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(AddSessionId, "Ryan Novak added to roster as Goalie"), Times.Once);
        _mockSubscriptionHandler.Verify(x => x.HandleUpdate(updated), Times.Once);
        sent.Should().ContainSingle();
    }

    [Theory]
    [InlineData(PositionPreference.Forward, TeamAssignment.Light, "Ryan Novak added to roster as Forward on Light")]
    [InlineData(PositionPreference.Defense, TeamAssignment.Dark, "Ryan Novak added to roster as Defense on Dark")]
    [InlineData(PositionPreference.TBD, TeamAssignment.Light, "Ryan Novak added to roster as TBD on Light")]
    public async Task AddRosterPlayer_Skater_StoresRequestedTeamAndLogsActivity(PositionPreference position, TeamAssignment team, string expectedMessage)
    {
        // Arrange
        SetupAddUser();
        var updated = EmptyRosterSession();
        SetupAddSuccess(EmptyRosterSession(), updated);

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(position, team));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(expectedMessage);
        _mockSessionRepository.Verify(x => x.AddRosterPlayerAsync(AddSessionId, AddUserId, team, position), Times.Once);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(AddSessionId, expectedMessage), Times.Once);
        _mockSubscriptionHandler.Verify(x => x.HandleUpdate(updated), Times.Once);
    }

    [Theory]
    [InlineData(PositionPreference.Forward, TeamAssignment.Light)]
    [InlineData(PositionPreference.Goalie, TeamAssignment.TBD)]
    public async Task AddRosterPlayer_SkaterOrGoalie_SendsSameAddedToRosterMessageOnce(PositionPreference position, TeamAssignment team)
    {
        // Arrange
        var user = SetupAddUser();
        var session = EmptyRosterSession();
        var sent = SetupAddSuccess(session, EmptyRosterSession());

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(position, team));

        // Assert: identical shape to DeletedFromRoster, no position-specific data (D10)
        result.IsSuccess.Should().BeTrue();
        var (message, subject) = sent.Should().ContainSingle().Subject;
        subject.Should().Be("AddedToRoster");
        message.Metadata["Type"].Should().Be("AddedToRoster");
        message.CommunicationMethod["Email"].Should().Be(user.Email);
        message.CommunicationMethod["NotificationPreference"].Should().Be(NotificationPreference.All.ToString());
        message.RelatedEntities.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            { "UserId", AddUserId },
            { "FirstName", "Ryan" },
            { "LastName", "Novak" }
        });
        message.MessageData.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            { "SessionDate", session.SessionDate.ToString() },
            { "SessionUrl", "https://test.com/session/1" }
        });
        message.NotificationEmails.Should().Equal("watcher@example.com");
    }

    [Fact]
    public async Task AddRosterPlayer_InactiveUser_IsAllowedAndLogsInformation()
    {
        // Arrange
        SetupAddUser(active: false);
        SetupAddSuccess(EmptyRosterSession(), EmptyRosterSession());

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("inactive user addUser")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task AddRosterPlayer_ActiveUser_DoesNotLogInactiveNotice()
    {
        // Arrange
        SetupAddUser(active: true);
        SetupAddSuccess(EmptyRosterSession(), EmptyRosterSession());

        // Act
        await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public async Task AddRosterPlayer_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManager.Setup(x => x.FindByIdAsync(AddUserId)).ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
        _mockSessionRepository.Verify(x => x.GetSessionAsync(It.IsAny<int>()), Times.Never);
        VerifyNothingAdded();
    }

    [Fact]
    public async Task AddRosterPlayer_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        SetupAddUser();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(AddSessionId)).ReturnsAsync((SessionDetailedResponse) null!);

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Session not found");
        VerifyNothingAdded();
    }

    [Theory]
    [InlineData(PositionPreference.Goalie)]
    [InlineData(PositionPreference.Forward)]
    public async Task AddRosterPlayer_AlreadyPlaying_ReturnsFailure(PositionPreference existingPosition)
    {
        // Arrange
        SetupAddUser();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(AddSessionId)).ReturnsAsync(CreateTestSession(AddUserId, (int) existingPosition, 0, isPlaying: true));

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Ryan Novak is already on the roster for this session");
        VerifyNothingAdded();
    }

    [Fact]
    public async Task AddRosterPlayer_NotPlayingRowExists_ReturnsFailureWithoutReactivating()
    {
        // Arrange
        SetupAddUser();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(AddSessionId)).ReturnsAsync(CreateTestSession(AddUserId, 1, 1, isPlaying: false));

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Forward, TeamAssignment.Light));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Ryan Novak has a not-playing roster entry for this session; use playing status instead");
        _mockSessionRepository.Verify(x => x.UpdatePlayerStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
        VerifyNothingAdded();
    }

    [Fact]
    public async Task AddRosterPlayer_OtherPlayersOnRoster_AddsNewPlayer()
    {
        // Arrange
        SetupAddUser();
        SetupAddSuccess(CreateTestSession("someoneElse", 1, 1), EmptyRosterSession());

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Defense, TeamAssignment.Dark));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSessionRepository.Verify(x => x.AddRosterPlayerAsync(AddSessionId, AddUserId, TeamAssignment.Dark, PositionPreference.Defense), Times.Once);
    }

    [Fact]
    public async Task AddRosterPlayer_NullCurrentRosters_AddsPlayer()
    {
        // Arrange
        SetupAddUser();
        var session = EmptyRosterSession();
        session.CurrentRosters = null;
        SetupAddSuccess(session, EmptyRosterSession());

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(PositionPreference.Forward)]
    [InlineData(PositionPreference.Defense)]
    [InlineData(PositionPreference.TBD)]
    public async Task AddRosterPlayer_SkaterWithoutLightOrDark_ReturnsFailure(PositionPreference position)
    {
        // Arrange
        SetupAddUser();
        _mockSessionRepository.Setup(x => x.GetSessionAsync(AddSessionId)).ReturnsAsync(EmptyRosterSession());

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(position, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("A skater must be assigned to Light or Dark");
        VerifyNothingAdded();
    }

    [Fact]
    public async Task AddRosterPlayer_RepositoryThrowsDuplicate_ReturnsFailureAndSendsNothing()
    {
        // Arrange
        SetupAddUser();
        var sent = SetupAddSuccess(EmptyRosterSession(), EmptyRosterSession());
        _mockSessionRepository.Setup(x => x.AddRosterPlayerAsync(AddSessionId, AddUserId, It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>()))
            .ThrowsAsync(new InvalidOperationException("Player already has a roster entry for this session"));

        // Act
        var result = await _sessionService.AddRosterPlayer(AddRequest(PositionPreference.Goalie, TeamAssignment.TBD));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("An error occurred adding player to roster: Player already has a roster entry for this session");
        sent.Should().BeEmpty();
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _mockSubscriptionHandler.Verify(x => x.HandleUpdate(It.IsAny<SessionDetailedResponse>()), Times.Never);
    }

    private void VerifyNothingAdded()
    {
        _mockSessionRepository.Verify(x => x.AddRosterPlayerAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>(), It.IsAny<PositionPreference>()), Times.Never);
        _mockSessionRepository.Verify(x => x.AddActivityAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _serviceBus.Verify(x => x.SendAsync(It.IsAny<ServiceBusCommsMessage>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterTeam_Goalie_ReturnsFailure()
    {
        // Arrange
        var userId = "goalieUser";
        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(new AspNetUser { Id = userId, FirstName = "Ken", LastName = "Ornstein" });
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(CreateTestSession(userId, (int) PositionPreference.Goalie, 0));

        // Act
        var result = await _sessionService.UpdateRosterTeam(1, userId, TeamAssignment.Light);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Goalies are not assigned to a team");
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_ToGoalie_SetsPositionAndTbdTeamInOneCall()
    {
        // Arrange
        var userId = "skaterUser";
        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(new AspNetUser { Id = userId, FirstName = "Ken", LastName = "Ornstein" });
        var session = CreateTestSession(userId, (int) PositionPreference.Forward, (int) TeamAssignment.Dark);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockSessionRepository.Setup(x => x.UpdatePlayerPositionAndTeamAsync(1, userId, PositionPreference.Goalie, TeamAssignment.TBD)).ReturnsAsync(session);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, userId, PositionPreference.Goalie);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Ken Ornstein changed position from Forward to Goalie");
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAndTeamAsync(1, userId, PositionPreference.Goalie, TeamAssignment.TBD), Times.Once);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>()), Times.Never);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRosterPosition_FromGoalieToSkater_KeepsTbdAndAsksForTeam()
    {
        // Arrange
        var userId = "goalieUser";
        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(new AspNetUser { Id = userId, FirstName = "Ken", LastName = "Ornstein" });
        var session = CreateTestSession(userId, (int) PositionPreference.Goalie, (int) TeamAssignment.TBD);
        _mockSessionRepository.Setup(x => x.GetSessionAsync(1)).ReturnsAsync(session);
        _mockSessionRepository.Setup(x => x.UpdatePlayerPositionAsync(1, userId, PositionPreference.Defense)).ReturnsAsync(session);
        _mockSessionRepository.Setup(x => x.AddActivityAsync(1, It.IsAny<string>())).ReturnsAsync(session);

        // Act
        var result = await _sessionService.UpdateRosterPosition(1, userId, PositionPreference.Defense);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Ken Ornstein changed position from Goalie to Defense (assign a team)");
        _mockSessionRepository.Verify(x => x.AddActivityAsync(1, "Ken Ornstein changed position from Goalie to Defense (assign a team)"), Times.Once);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAsync(1, userId, PositionPreference.Defense), Times.Once);
        _mockSessionRepository.Verify(x => x.UpdatePlayerTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TeamAssignment>()), Times.Never);
        _mockSessionRepository.Verify(x => x.UpdatePlayerPositionAndTeamAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PositionPreference>(), It.IsAny<TeamAssignment>()), Times.Never);
    }
}
