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
using HockeyPickup.Api.Services;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;

namespace HockeyPickup.Api.Tests.ControllerTests;

public partial class UsersControllerTest
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ILogger<UsersController>> _mockLogger;
    private readonly UsersController _controller;

    public UsersControllerTest()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUserService = new Mock<IUserService>();
        _mockLogger = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_mockUserRepository.Object, _mockLogger.Object, _mockUserService.Object);
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
    public async Task GetUsers_RepositoryThrowsException_Returns500()
    {
        // Arrange
        SetupUserRole("User");
        _mockUserRepository
            .Setup(x => x.GetDetailedUsersAsync())
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

        var expectedUser = new UserDetailedResponse
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Preferred = true,
            PreferredPlus = false,
            Active = true,
            Rating = 1
        };

        _mockUserRepository
            .Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _controller.GetUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedUser = okResult.Value.Should().BeOfType<UserDetailedResponse>().Subject;
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

public partial class UsersControllerTest
{
    [Fact]
    public async Task GetUserById_ValidId_ReturnsOkWithUser()
    {
        // Arrange
        var userId = "test-user-id";
        var expectedUser = new UserDetailedResponse
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Preferred = true,
            PreferredPlus = false,
            Active = true,
            Rating = 1
        };

        _mockUserRepository
            .Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _controller.GetUserById(userId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedUser = okResult.Value.Should().BeOfType<UserDetailedResponse>().Subject;
        returnedUser.Should().BeEquivalentTo(expectedUser);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public async Task GetUserById_InvalidId_ReturnsBadRequest(string? userId)
    {
        // Act
        var result = await _controller.GetUserById(userId!);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<ApiErrorResponse>(
            JsonSerializer.Serialize(badRequestResult.Value),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        response.Should().NotBeNull();
        response!.Message.Should().Be("User ID cannot be empty");
    }

    [Fact]
    public async Task GetUserById_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = "non-existent-id";
        _mockUserRepository
            .Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync((UserDetailedResponse) null!);

        // Act
        var result = await _controller.GetUserById(userId);

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
    public async Task GetUserById_RepositoryThrowsException_Returns500()
    {
        // Arrange
        var userId = "test-user-id";
        _mockUserRepository
            .Setup(x => x.GetUserAsync(userId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetUserById(userId);

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

public partial class UsersControllerTest
{
    private static UserPaymentMethodResponse CreateSamplePaymentMethod(int id = 1)
    {
        return new UserPaymentMethodResponse
        {
            UserPaymentMethodId = id,
            MethodType = PaymentMethodType.PayPal,
            Identifier = "test@example.com",
            PreferenceOrder = 1,
            IsActive = true
        };
    }

    [Fact]
    public async Task AddPaymentMethod_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var userId = "test-user-id";
        var request = new UserPaymentMethodRequest
        {
            MethodType = PaymentMethodType.PayPal,
            Identifier = "test@example.com",
            PreferenceOrder = 1,
            IsActive = true
        };
        var expectedResponse = CreateSamplePaymentMethod();

        _mockUserService
            .Setup(x => x.AddUserPaymentMethodAsync(userId, request))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateSuccess(expectedResponse));

        // Act
        var result = await _controller.AddPaymentMethod(userId, request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(UsersController.GetPaymentMethod));
        createdResult.RouteValues!["userId"].Should().Be(userId);
        createdResult.RouteValues!["paymentMethodId"].Should().Be(expectedResponse.UserPaymentMethodId);

        var response = createdResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task AddPaymentMethod_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var request = new UserPaymentMethodRequest
        {
            MethodType = PaymentMethodType.PayPal,
            Identifier = "test@example.com",
            PreferenceOrder = 1,
            IsActive = true
        };

        _mockUserService
            .Setup(x => x.AddUserPaymentMethodAsync(userId, request))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateFailure("Invalid request"));

        // Act
        var result = await _controller.AddPaymentMethod(userId, request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Message.Should().Be("Invalid request");
    }

    [Fact]
    public async Task UpdatePaymentMethod_ValidRequest_ReturnsOk()
    {
        // Arrange
        var userId = "test-user-id";
        var paymentMethodId = 1;
        var request = new UserPaymentMethodRequest
        {
            MethodType = PaymentMethodType.PayPal,
            Identifier = "updated@example.com",
            PreferenceOrder = 2,
            IsActive = true
        };
        var expectedResponse = CreateSamplePaymentMethod(paymentMethodId);

        _mockUserService
            .Setup(x => x.UpdateUserPaymentMethodAsync(userId, paymentMethodId, request))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateSuccess(expectedResponse));

        // Act
        var result = await _controller.UpdatePaymentMethod(userId, paymentMethodId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task GetPaymentMethods_ValidRequest_ReturnsOk()
    {
        // Arrange
        var userId = "test-user-id";
        var expectedMethods = new List<UserPaymentMethodResponse>
        {
            CreateSamplePaymentMethod(1),
            CreateSamplePaymentMethod(2)
        };

        _mockUserService
            .Setup(x => x.GetUserPaymentMethodsAsync(userId))
            .ReturnsAsync(ServiceResult<IEnumerable<UserPaymentMethodResponse>>.CreateSuccess(expectedMethods));

        // Act
        var result = await _controller.GetPaymentMethods(userId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>>().Subject;
        response.Data.Should().BeEquivalentTo(expectedMethods);
    }

    [Fact]
    public async Task GetPaymentMethod_ValidRequest_ReturnsOk()
    {
        // Arrange
        var userId = "test-user-id";
        var paymentMethodId = 1;
        var expectedMethod = CreateSamplePaymentMethod(paymentMethodId);

        _mockUserService
            .Setup(x => x.GetUserPaymentMethodAsync(userId, paymentMethodId))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateSuccess(expectedMethod));

        // Act
        var result = await _controller.GetPaymentMethod(userId, paymentMethodId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expectedMethod);
    }

    [Fact]
    public async Task DeletePaymentMethod_ValidRequest_ReturnsOk()
    {
        // Arrange
        var userId = "test-user-id";
        var paymentMethodId = 1;
        var deletedMethod = CreateSamplePaymentMethod(paymentMethodId);

        _mockUserService
            .Setup(x => x.DeleteUserPaymentMethodAsync(userId, paymentMethodId))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateSuccess(deletedMethod));

        // Act
        var result = await _controller.DeletePaymentMethod(userId, paymentMethodId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(deletedMethod);
    }

    [Fact]
    public async Task DeletePaymentMethod_NotFound_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var paymentMethodId = 1;

        _mockUserService
            .Setup(x => x.DeleteUserPaymentMethodAsync(userId, paymentMethodId))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateFailure("Payment method not found"));

        // Act
        var result = await _controller.DeletePaymentMethod(userId, paymentMethodId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Message.Should().Be("Payment method not found");
    }

    [Fact]
    public async Task UpdatePaymentMethod_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var paymentMethodId = 1;
        var request = CreatePaymentMethodRequest();

        _mockUserService
            .Setup(x => x.UpdateUserPaymentMethodAsync(userId, paymentMethodId, request))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateFailure("Update failed"));

        // Act
        var result = await _controller.UpdatePaymentMethod(userId, paymentMethodId, request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task GetPaymentMethods_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";

        _mockUserService
            .Setup(x => x.GetUserPaymentMethodsAsync(userId))
            .ReturnsAsync(ServiceResult<IEnumerable<UserPaymentMethodResponse>>.CreateFailure("Get failed"));

        // Act
        var result = await _controller.GetPaymentMethods(userId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<IEnumerable<UserPaymentMethodResponse>>>().Subject;
        response.Message.Should().Be("Get failed");
    }

    [Fact]
    public async Task GetPaymentMethod_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var paymentMethodId = 1;

        _mockUserService
            .Setup(x => x.GetUserPaymentMethodAsync(userId, paymentMethodId))
            .ReturnsAsync(ServiceResult<UserPaymentMethodResponse>.CreateFailure("Get failed"));

        // Act
        var result = await _controller.GetPaymentMethod(userId, paymentMethodId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<UserPaymentMethodResponse>>().Subject;
        response.Message.Should().Be("Get failed");
    }

    private static UserPaymentMethodRequest CreatePaymentMethodRequest()
    {
        return new UserPaymentMethodRequest
        {
            MethodType = PaymentMethodType.PayPal,
            Identifier = "test@example.com",
            PreferenceOrder = 1,
            IsActive = true
        };
    }
}
