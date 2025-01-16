using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.GraphQL;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.GraphQLTests;

public class RegularGraphQLTests
{
    private readonly DateTime _testDate = DateTime.UtcNow;

    private static UserDetailedResponse CreateTestUser(string id, string userName) =>
        new()
        {
            Id = id,
            UserName = userName,
            Email = $"{userName}@test.com",
            FirstName = "Test",
            LastName = "User",
            Rating = 4.5m,
            Preferred = true,
            PreferredPlus = false,
            Active = true,
            NotificationPreference = NotificationPreference.All,
            PositionPreference = PositionPreference.TBD,
            DateCreated = DateTime.UtcNow,
            Roles = []
        };

    [Fact]
    public async Task RegularSets_ShouldReturnAllSets_WhenDataExists()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var regularRepositoryMock = new Mock<IRegularRepository>();

        var expectedRegularSets = new List<RegularSetDetailedResponse>
        {
            new()
            {
                RegularSetId = 1,
                Description = "Monday Night",
                DayOfWeek = 1,
                CreateDateTime = _testDate,
                Archived = false,
                Regulars = new List<RegularDetailedResponse>
                {
                    new()
                    {
                        RegularSetId = 1,
                        UserId = "user1",
                        TeamAssignment = 1,
                        PositionPreference = 2,
                        User = CreateTestUser("user1", "john.doe")
                    }
                }
            }
        };

        regularRepositoryMock
            .Setup(repo => repo.GetRegularSetsAsync())
            .ReturnsAsync(expectedRegularSets);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.GetRegularSets(regularRepositoryMock.Object);

        // Assert
        var resultList = result.ToList();
        resultList.Should().NotBeEmpty();
        resultList.Should().HaveCount(1);
        resultList[0].RegularSetId.Should().Be(1);
        resultList[0].Description.Should().Be("Monday Night");
        resultList[0].Regulars.Should().HaveCount(1);
        resultList[0].Regulars[0].User.Should().NotBeNull();
        resultList[0].Regulars[0].User!.FirstName.Should().Be("Test");
        regularRepositoryMock.Verify(r => r.GetRegularSetsAsync(), Times.Once);
    }

    [Fact]
    public async Task RegularSets_ShouldReturnEmptyList_WhenNoData()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var regularRepositoryMock = new Mock<IRegularRepository>();

        regularRepositoryMock
            .Setup(repo => repo.GetRegularSetsAsync())
            .ReturnsAsync(new List<RegularSetDetailedResponse>());

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.GetRegularSets(regularRepositoryMock.Object);

        // Assert
        result.Should().BeEmpty();
        regularRepositoryMock.Verify(r => r.GetRegularSetsAsync(), Times.Once);
    }

    [Fact]
    public async Task RegularSet_ShouldReturnSpecificSet_WhenExists()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var regularRepositoryMock = new Mock<IRegularRepository>();

        var expectedRegularSet = new RegularSetDetailedResponse
        {
            RegularSetId = 1,
            Description = "Monday Night",
            DayOfWeek = 1,
            CreateDateTime = _testDate,
            Archived = false,
            Regulars = new List<RegularDetailedResponse>
            {
                new()
                {
                    RegularSetId = 1,
                    UserId = "user1",
                    TeamAssignment = 1,
                    PositionPreference = 2,
                    User = CreateTestUser("user1", "john.doe")
                }
            }
        };

        regularRepositoryMock
            .Setup(repo => repo.GetRegularSetAsync(1))
            .ReturnsAsync(expectedRegularSet);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.GetRegularSet(1, regularRepositoryMock.Object);

        // Assert
        result.Should().NotBeNull();
        result!.RegularSetId.Should().Be(1);
        result.Description.Should().Be("Monday Night");
        result.Regulars.Should().HaveCount(1);
        result.Regulars[0].User.Should().NotBeNull();
        result.Regulars[0].User!.FirstName.Should().Be("Test");
        regularRepositoryMock.Verify(r => r.GetRegularSetAsync(1), Times.Once);
    }

    [Fact]
    public async Task RegularSet_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<Query>>();
        var regularRepositoryMock = new Mock<IRegularRepository>();

        regularRepositoryMock
            .Setup(repo => repo.GetRegularSetAsync(999))
            .ReturnsAsync((RegularSetDetailedResponse?) null);

        var query = new Query(httpContextAccessorMock.Object, loggerMock.Object);

        // Act
        var result = await query.GetRegularSet(999, regularRepositoryMock.Object);

        // Assert
        result.Should().BeNull();
        regularRepositoryMock.Verify(r => r.GetRegularSetAsync(999), Times.Once);
    }
}
