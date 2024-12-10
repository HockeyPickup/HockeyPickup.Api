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
    [Fact]
    public async Task UsersEx_ShouldReturnDetailedUsers_WhenAdmin()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();

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
}
