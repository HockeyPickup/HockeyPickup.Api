using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.GraphQL;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.GraphQLTests;

public class GraphQLTests
{
    private readonly DateTime _testDate = DateTime.UtcNow;

    [Fact]
    public async Task UsersEx_ShouldReturnDetailedUsers_WhenAdmin()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();
        var sessionRepositoryMock = new Mock<ISessionRepository>();

        var expectedUsers = new List<UserDetailedResponse>
        {
            new UserDetailedResponse
            {
                Id = "user123",
                UserName = "adminUser",
                Active = true,
                Preferred = true,
                PreferredPlus = true,
                Rating = 4.5m
            }
        };

        userRepositoryMock.Setup(repo => repo.GetDetailedUsersAsync())
            .ReturnsAsync(expectedUsers);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.UsersEx(userRepositoryMock.Object);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal("user123", resultList[0].Id);
        Assert.Equal("adminUser", resultList[0].UserName);
        Assert.Equal(4.5m, resultList[0].Rating);
        userRepositoryMock.Verify(r => r.GetDetailedUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task Users_ShouldReturnEmptyList_WhenNoUsers()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock.Setup(repo => repo.GetDetailedUsersAsync())
            .ReturnsAsync(new List<UserDetailedResponse>());

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.UsersEx(userRepositoryMock.Object);

        // Assert
        Assert.Empty(result);
        userRepositoryMock.Verify(r => r.GetDetailedUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task UsersEx_ShouldReturnEmptyList_WhenNoDetailedUsers()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock.Setup(repo => repo.GetDetailedUsersAsync())
            .ReturnsAsync(new List<UserDetailedResponse>());

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.UsersEx(userRepositoryMock.Object);

        // Assert
        Assert.Empty(result);
        userRepositoryMock.Verify(r => r.GetDetailedUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task LockerRoom13_ShouldReturnSessions_WhenDataExists()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();
        var testDate = DateTime.UtcNow.AddDays(1);
        var expectedResponse = new List<LockerRoom13Response>
        {
            new()
            {
                SessionId = 1,
                SessionDate = testDate,
                LockerRoom13Players = new List<LockerRoom13Players>
                {
                    new()
                    {
                        Id = "user1",
                        UserName = "user1@test.com",
                        Email = "user1@test.com",
                        FirstName = "Test",
                        LastName = "User",
                        Active = true,
                        Preferred = true,
                        PreferredPlus = false,
                        LockerRoom13 = true,
                        PlayerStatus = PlayerStatus.Regular
                    }
                }
            }
        };

        userRepositoryMock.Setup(repo => repo.GetLockerRoom13SessionsAsync())
            .ReturnsAsync(expectedResponse);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.LockerRoom13(userRepositoryMock.Object);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal(1, resultList[0].SessionId);
        Assert.Equal(testDate, resultList[0].SessionDate);
        Assert.Single(resultList[0].LockerRoom13Players);
        Assert.Equal("user1", resultList[0].LockerRoom13Players[0].Id);
        userRepositoryMock.Verify(r => r.GetLockerRoom13SessionsAsync(), Times.Once);
    }

    [Fact]
    public async Task LockerRoom13_ShouldReturnEmptyList_WhenNoSessions()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock.Setup(repo => repo.GetLockerRoom13SessionsAsync())
            .ReturnsAsync(new List<LockerRoom13Response>());

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.LockerRoom13(userRepositoryMock.Object);

        // Assert
        Assert.Empty(result);
        userRepositoryMock.Verify(r => r.GetLockerRoom13SessionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetSessions_CallsRepository()
    {
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var sessionRepositoryMock = new Mock<ISessionRepository>();
        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Arrange
        var expectedSessions = new List<SessionBasicResponse>
        {
            new() {
                SessionId = 1,
                Note = "Test Session 1",
                CreateDateTime = _testDate,
                UpdateDateTime = _testDate,
                SessionDate = _testDate.AddDays(7)
            }
        };
        sessionRepositoryMock.Setup(r => r.GetBasicSessionsAsync())
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await query.GetSessions(sessionRepositoryMock.Object);

        // Assert
        result.Should().BeEquivalentTo(expectedSessions);
        sessionRepositoryMock.Verify(r => r.GetBasicSessionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetSession_CallsRepository()
    {
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var sessionRepositoryMock = new Mock<ISessionRepository>();
        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Arrange
        var expectedSession = new SessionDetailedResponse
        {
            SessionId = 1,
            Note = "Test Session",
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            SessionDate = _testDate.AddDays(7),
            BuySells = new List<BuySellResponse>(),
            ActivityLogs = new List<ActivityLogResponse>(),
            BuyingQueues = new List<BuyingQueueItem>(),
            CurrentRosters = new List<Models.Responses.RosterPlayer>()
        };
        sessionRepositoryMock.Setup(r => r.GetSessionAsync(1))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await query.GetSession(1, sessionRepositoryMock.Object);

        // Assert
        result.Should().BeEquivalentTo(expectedSession);
        sessionRepositoryMock.Verify(r => r.GetSessionAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetUserStats_ReturnsUserStats()
    {
        // Arrange 
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();
        var testDate = DateTime.UtcNow;

        var expectedStats = new UserStatsResponse
        {
            MemberSince = testDate.AddYears(-1),
            CurrentYearGamesPlayed = 5,
            PriorYearGamesPlayed = 10,
            CurrentYearBoughtTotal = 2,
            PriorYearBoughtTotal = 3,
            LastBoughtSessionDate = testDate,
            CurrentYearSoldTotal = 1,
            PriorYearSoldTotal = 2,
            LastSoldSessionDate = testDate.AddDays(-7),
            MostPlayedPosition = "Defense",
            CurrentBuyRequests = 1
        };

        userRepositoryMock.Setup(repo => repo.GetUserStatsAsync("user1"))
            .ReturnsAsync(expectedStats);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.GetUserStats("user1", userRepositoryMock.Object);

        // Assert
        result.Should().BeEquivalentTo(expectedStats);
        userRepositoryMock.Verify(r => r.GetUserStatsAsync("user1"), Times.Once);
    }

    [Fact]
    public async Task GetUserStats_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock.Setup(repo => repo.GetUserStatsAsync("nonexistent"))
            .ReturnsAsync((UserStatsResponse?) null);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.GetUserStats("nonexistent", userRepositoryMock.Object);

        // Assert
        result.Should().BeNull();
        userRepositoryMock.Verify(r => r.GetUserStatsAsync("nonexistent"), Times.Once);
    }
}
