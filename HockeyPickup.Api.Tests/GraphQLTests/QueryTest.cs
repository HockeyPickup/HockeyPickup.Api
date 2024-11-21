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
    public async Task Users_ShouldReturnBasicUsers()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var userRepositoryMock = new Mock<IUserRepository>();

        var expectedUsers = new List<UserBasicResponse>
        {
            new UserBasicResponse
            {
                Id = "user123",
                UserName = "basicUser",
                Active = true,
                Preferred = false,
                PreferredPlus = false
            }
        };

        userRepositoryMock.Setup(repo => repo.GetBasicUsersAsync())
            .ReturnsAsync(expectedUsers);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.Users(userRepositoryMock.Object);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal("user123", resultList[0].Id);
        Assert.Equal("basicUser", resultList[0].UserName);
        userRepositoryMock.Verify(r => r.GetBasicUsersAsync(), Times.Once);
    }

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

        userRepositoryMock.Setup(repo => repo.GetBasicUsersAsync())
            .ReturnsAsync(new List<UserBasicResponse>());

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.Users(userRepositoryMock.Object);

        // Assert
        Assert.Empty(result);
        userRepositoryMock.Verify(r => r.GetBasicUsersAsync(), Times.Once);
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
}
