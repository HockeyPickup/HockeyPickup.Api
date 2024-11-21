using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Responses;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace HockeyPickup.Api.Tests.ControllerTests;

public partial class UsersControllerTest
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<UsersController>> _mockLogger;
    private readonly UsersController _controller;

    public UsersControllerTest()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_mockUserRepository.Object, _mockLogger.Object);
    }

    private class ApiErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    private void SetupUserRole(string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "test@example.com"),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static List<UserDetailedResponse> CreateDetailedUsersList()
    {
        return new List<UserDetailedResponse>
        {
            new() {
                Id = "1",
                UserName = "user1",
                Email = "user1@example.com",
                FirstName = "John",
                LastName = "Doe",
                Preferred = true,
                PreferredPlus = false,
                Rating = 4.5m,
                Active = true
            },
            new() {
                Id = "2",
                UserName = "user2",
                Email = "user2@example.com",
                FirstName = "Jane",
                LastName = "Smith",
                Preferred = true,
                PreferredPlus = true,
                Rating = 4.8m,
                Active = true
            }
        };
    }

    private static List<UserBasicResponse> CreateBasicUsersList()
    {
        return new List<UserBasicResponse>
        {
            new() {
                Id = "1",
                UserName = "user1",
                Email = "user1@example.com",
                FirstName = "John",
                LastName = "Doe",
                Preferred = true,
                PreferredPlus = false,
                Active = true
            },
            new() {
                Id = "2",
                UserName = "user2",
                Email = "user2@example.com",
                FirstName = "Jane",
                LastName = "Smith",
                Preferred = true,
                PreferredPlus = true,
                Active = true
            }
        };
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsDetailedUsers()
    {
        // Arrange
        SetupUserRole("Admin");
        var detailedUsers = CreateDetailedUsersList();

        _mockUserRepository
            .Setup(x => x.GetDetailedUsersAsync())
            .ReturnsAsync(detailedUsers);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedUsers = okResult.Value.Should().BeAssignableTo<IEnumerable<UserDetailedResponse>>().Subject;
        returnedUsers.Should().BeEquivalentTo(detailedUsers);
    }

    [Fact]
    public async Task GetUsers_AsRegularUser_ReturnsBasicUsers()
    {
        // Arrange
        SetupUserRole("User");
        var basicUsers = CreateBasicUsersList();

        _mockUserRepository
            .Setup(x => x.GetBasicUsersAsync())
            .ReturnsAsync(basicUsers);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedUsers = okResult.Value.Should().BeAssignableTo<IEnumerable<UserBasicResponse>>().Subject;
        returnedUsers.Should().BeEquivalentTo(basicUsers);
    }

    [Fact]
    public async Task GetUsers_RepositoryThrowsException_Returns500()
    {
        // Arrange
        SetupUserRole("User");
        _mockUserRepository
            .Setup(x => x.GetBasicUsersAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);

        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(statusCodeResult.Value)
        );
        response!["message"].Should().Be("An error occurred while retrieving users");

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetUsers_NoRoleClaim_ReturnsBasicUsers()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "test@example.com")
            // No role claim
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var basicUsers = CreateBasicUsersList();

        _mockUserRepository
            .Setup(x => x.GetBasicUsersAsync())
            .ReturnsAsync(basicUsers);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedUsers = okResult.Value.Should().BeAssignableTo<IEnumerable<UserBasicResponse>>().Subject;
        returnedUsers.Should().BeEquivalentTo(basicUsers);
    }
}

public partial class UsersControllerTest
{
    private void SetupUser(string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetUser_ValidUser_ReturnsOkWithUser()
    {
        // Arrange
        var userId = "test-user-id";
        SetupUser(userId);

        var expectedUser = new UserBasicResponse
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Preferred = true,
            PreferredPlus = false,
            Active = true
        };

        _mockUserRepository
            .Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _controller.GetUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedUser = okResult.Value.Should().BeOfType<UserBasicResponse>().Subject;
        returnedUser.Should().BeEquivalentTo(expectedUser);
    }

    [Fact]
    public async Task GetUser_NoUserIdClaim_ReturnsNotFound()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        // Act
        var result = await _controller.GetUser();

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<ApiErrorResponse>(
            JsonSerializer.Serialize(notFoundResult.Value),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        response.Should().NotBeNull();
        response!.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task GetUser_RepositoryThrowsException_Returns500()
    {
        // Arrange
        var userId = "test-user-id";
        SetupUser(userId);

        _mockUserRepository
            .Setup(x => x.GetUserAsync(userId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetUser();

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);

        var response = JsonSerializer.Deserialize<ApiErrorResponse>(
            JsonSerializer.Serialize(statusCodeResult.Value),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        response.Should().NotBeNull();
        response!.Message.Should().Be("An error occurred while retrieving user");

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
