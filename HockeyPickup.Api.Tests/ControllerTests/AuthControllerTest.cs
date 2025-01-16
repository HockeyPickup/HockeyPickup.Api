using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Services;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Helpers;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using HockeyPickup.Api.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Tests.ControllerTests;

public partial class AuthControllerTest
{
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ITokenBlacklistService> _mockTokenBlacklist;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTest()
    {
        _mockJwtService = new Mock<IJwtService>();
        _mockUserService = new Mock<IUserService>();
        _mockTokenBlacklist = new Mock<ITokenBlacklistService>();
        _mockLogger = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _mockJwtService.Object,
            _mockUserService.Object,
            _mockTokenBlacklist.Object,
            _mockLogger.Object
        );
    }

    private void SetupAuthentication(bool isAuthenticated = true)
    {
        var httpContext = new DefaultHttpContext();

        if (isAuthenticated)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Name, "test@example.com")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            httpContext.User = claimsPrincipal;
        }

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            UserName = "user@example.com",
            Password = "validPassword123!"
        };

        var user = new User { Id = "123", UserName = "user@example.com" };
        var roles = new[] { "User" };

        _mockUserService
            .Setup(x => x.ValidateCredentialsAsync(request.UserName, request.Password))
            .ReturnsAsync(ServiceResult<(User user, string[] roles)>.CreateSuccess((user, roles)));

        var token = "valid.jwt.token";
        var expiration = DateTime.UtcNow.AddHours(1);

        _mockJwtService
            .Setup(x => x.GenerateToken(user.Id, user.UserName, roles))
            .Returns((token, expiration));

        // Act
        var actionResult = await _controller.Login(request);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<LoginResponse>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Token.Should().Be(token);
        response.Data.Expiration.Should().Be(expiration);
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            UserName = "user@example.com",
            Password = "wrongPassword123!"
        };

        _mockUserService
            .Setup(x => x.ValidateCredentialsAsync(request.UserName, request.Password))
            .ReturnsAsync(ServiceResult<(User user, string[] roles)>.CreateFailure("Invalid credentials"));

        // Act
        var actionResult = await _controller.Login(request);

        // Assert
        var unauthorizedResult = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<ApiDataResponse<LoginResponse>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid credentials");
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Login_UserServiceThrowsException_ThrowsException()
    {
        // Arrange
        var request = new LoginRequest
        {
            UserName = "user@example.com",
            Password = "password123!"
        };

        _mockUserService
            .Setup(x => x.ValidateCredentialsAsync(request.UserName, request.Password))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _controller.Login(request));
    }

    [Fact]
    public async Task Logout_ValidToken_ReturnsOkResult()
    {
        // Arrange
        SetupAuthentication();
        var token = "valid.jwt.token";
        _controller.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

        _mockTokenBlacklist
            .Setup(x => x.IsTokenBlacklistedAsync(token))
            .ReturnsAsync(false);

        _mockTokenBlacklist
            .Setup(x => x.InvalidateTokenAsync(token))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Logged out successfully");

        // Verify the token was invalidated
        _mockTokenBlacklist.Verify(x => x.InvalidateTokenAsync(token), Times.Once);
    }

    [Fact]
    public async Task Logout_NoToken_ReturnsBadRequest()
    {
        // Arrange
        SetupAuthentication();

        // Act
        var result = await _controller.Logout();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("No token found");
    }

    [Fact]
    public async Task Logout_AlreadyBlacklistedToken_ReturnsBadRequest()
    {
        // Arrange
        SetupAuthentication();
        var token = "already.blacklisted.token";
        _controller.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

        _mockTokenBlacklist
            .Setup(x => x.IsTokenBlacklistedAsync(token))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Logout();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Token already invalidated");

        // Verify we didn't try to invalidate an already invalid token
        _mockTokenBlacklist.Verify(x => x.InvalidateTokenAsync(It.IsAny<string>()), Times.Never);
    }
}

public partial class AuthControllerTest
{
    [Fact]
    public async Task Register_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        var user = new AspNetUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult<AspNetUser>.CreateSuccess(user));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<AspNetUser>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Email.Should().Be(request.Email);
        response.Data.FirstName.Should().Be(request.FirstName);
        response.Data.LastName.Should().Be(request.LastName);
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult<AspNetUser>.CreateFailure("Email already exists"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<AspNetUser>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Email already exists");
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be("Email already exists");
    }

    [Fact]
    public async Task ConfirmEmail_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidConfirmEmailRequest();

        _mockUserService
            .Setup(x => x.ConfirmEmailAsync(request.Email, request.Token))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmEmail_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidConfirmEmailRequest();
        var errorMessage = "Invalid or expired token";

        _mockUserService
            .Setup(x => x.ConfirmEmailAsync(request.Email, request.Token))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    private static RegisterRequest CreateValidRegisterRequest()
    {
        return new RegisterRequest
        {
            Email = "test@example.com",
            Password = "StrongP@ss123!",
            ConfirmPassword = "StrongP@ss123!",
            FirstName = "Test",
            LastName = "User",
            FrontendUrl = "https://example.com/confirm",
            InviteCode = "12345"
        };
    }

    private static ConfirmEmailRequest CreateValidConfirmEmailRequest()
    {
        return new ConfirmEmailRequest
        {
            Email = "test@example.com",
            Token = "valid-token"
        };
    }
}

public partial class AuthControllerTest
{
    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.ChangePasswordAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangePassword_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();
        var errorMessage = "Current password is incorrect";

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.ChangePasswordAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task ChangePassword_NoUserIdentity_ThrowsException()
    {
        // Arrange
        var request = CreateValidChangePasswordRequest();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _controller.ChangePassword(request));
    }

    [Fact]
    public async Task ForgotPassword_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidForgotPasswordRequest();

        _mockUserService
            .Setup(x => x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidForgotPasswordRequest();
        var errorMessage = "Email not found";

        _mockUserService
            .Setup(x => x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task ResetPassword_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();

        _mockUserService
            .Setup(x => x.ResetPasswordAsync(request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPassword_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var errorMessage = "Invalid or expired reset token";

        _mockUserService
            .Setup(x => x.ResetPasswordAsync(request))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    private static ChangePasswordRequest CreateValidChangePasswordRequest()
    {
        return new ChangePasswordRequest
        {
            CurrentPassword = "OldP@ssword123",
            NewPassword = "NewP@ssword456",
            ConfirmNewPassword = "NewP@ssword456"
        };
    }

    private static ForgotPasswordRequest CreateValidForgotPasswordRequest()
    {
        return new ForgotPasswordRequest
        {
            Email = "test@example.com",
            FrontendUrl = "https://example.com/reset-password"
        };
    }

    private static ResetPasswordRequest CreateValidResetPasswordRequest()
    {
        return new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "valid-reset-token",
            NewPassword = "NewP@ssword123",
            ConfirmPassword = "NewP@ssword123"
        };
    }

    private void SetupUserClaims(string userId = "test-user-id")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "test@example.com")
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
}

public partial class AuthControllerTest
{
    [Fact]
    public async Task SaveUser_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidSaveUserRequest();

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();

        // Verify service was called correctly
        _mockUserService.Verify(x => x.SaveUserAsync(userId, request), Times.Once);
    }

    [Fact]
    public async Task SaveUser_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidSaveUserRequest();
        var errorMessage = "Failed to update user profile";

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task SaveUser_NoUserIdentity_ThrowsException()
    {
        // Arrange
        var request = CreateValidSaveUserRequest();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _controller.SaveUser(request));
    }

    [Theory]
    [InlineData("")]  // Empty string
    [InlineData(" ")] // Whitespace
    public async Task SaveUser_InvalidEmergencyContact_ReturnsBadRequest(string invalidValue)
    {
        // Arrange
        var userId = "test-user-id";
        SetupUserClaims(userId);

        var request = new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = invalidValue,
            EmergencyPhone = invalidValue,
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            PositionPreference = PositionPreference.TBD
        };

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateFailure("Emergency contact information is required"));

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Errors.Should().ContainSingle();
    }

    [Theory]
    [InlineData(NotificationPreference.OnlyMyBuySell)]
    [InlineData(NotificationPreference.None)]
    public async Task SaveUser_ValidNotificationPreference_ReturnsOkResponse(NotificationPreference preference)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = preference
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(PositionPreference.TBD)]
    [InlineData(PositionPreference.Forward)]
    public async Task SaveUser_ValidPositionPreference_ReturnsOkResponse(PositionPreference preference)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            PositionPreference = preference
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    private static SaveUserRequest CreateValidSaveUserRequest()
    {
        return new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell
        };
    }
}

public partial class AuthControllerTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveUser_ActiveStatus_ReturnsOkResponse(bool isActive)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequestEx
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            Active = isActive
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();

        _mockUserService.Verify(x => x.SaveUserAsync(
            It.IsAny<string>(),
            It.Is<SaveUserRequestEx>(r => r.Active == isActive)),
            Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveUser_PreferredStatus_ReturnsOkResponse(bool isPreferred)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequestEx
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            Preferred = isPreferred
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();

        _mockUserService.Verify(x => x.SaveUserAsync(
            It.IsAny<string>(),
            It.Is<SaveUserRequestEx>(r => r.Preferred == isPreferred)),
            Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveUser_PreferredPlusStatus_ReturnsOkResponse(bool isPreferredPlus)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequestEx
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            PreferredPlus = isPreferredPlus
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();

        _mockUserService.Verify(x => x.SaveUserAsync(
            It.IsAny<string>(),
            It.Is<SaveUserRequestEx>(r => r.PreferredPlus == isPreferredPlus)),
            Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveUser_LockerRoom13Access_ReturnsOkResponse(bool hasAccess)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequestEx
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            LockerRoom13 = hasAccess
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();

        _mockUserService.Verify(x => x.SaveUserAsync(
            It.IsAny<string>(),
            It.Is<SaveUserRequestEx>(r => r.LockerRoom13 == hasAccess)),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    [InlineData(5)]
    public async Task SaveUser_Rating_ReturnsOkResponse(decimal rating)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequestEx
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            Rating = rating
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();

        _mockUserService.Verify(x => x.SaveUserAsync(
            It.IsAny<string>(),
            It.Is<SaveUserRequestEx>(r => r.Rating == rating)),
            Times.Once);
    }

    [Theory]
    [InlineData(-1)]    // Below minimum
    [InlineData(5.1)]   // Above maximum
    public async Task SaveUser_InvalidRating_ReturnsBadRequest(decimal invalidRating)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new SaveUserRequestEx
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            Rating = invalidRating
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateFailure($"Rating must be between 0 and 5"));

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be("Rating must be between 0 and 5");
    }
}

public partial class AuthControllerTest
{
    [Fact]
    public async Task AdminUpdateUser_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new AdminUserUpdateRequest
        {
            UserId = "test-user-id",
            FirstName = "John",
            LastName = "Doe",
            Rating = 4.5m
        };

        _mockUserService
            .Setup(x => x.AdminUpdateUserAsync(It.IsAny<AdminUserUpdateRequest>()))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.AdminUpdateUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AdminUpdateUser_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = new AdminUserUpdateRequest { UserId = "test-user-id" };
        SetupUserClaims(request.UserId);
        var errorMessage = "Failed to update user";

        _mockUserService
            .Setup(x => x.AdminUpdateUserAsync(It.IsAny<AdminUserUpdateRequest>()))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.AdminUpdateUser(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
    }
}

public partial class AuthControllerTest
{
    [Fact]
    public async Task UploadPhoto_ValidFile_ReturnsOkResponse()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateTestFormFile("test.jpg", "image/jpeg");
        var expectedResponse = new PhotoResponse
        {
            PhotoUrl = "https://example.com/photos/test.jpg",
            UpdateDateTime = DateTime.UtcNow
        };

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.UploadProfilePhotoAsync(userId, file))
            .ReturnsAsync(ServiceResult<PhotoResponse>.CreateSuccess(expectedResponse));

        // Act
        var result = await _controller.UploadPhoto(file);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<PhotoResponse>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.PhotoUrl.Should().Be(expectedResponse.PhotoUrl);
        response.Errors.Should().BeEmpty();

        _mockUserService.Verify(x => x.UploadProfilePhotoAsync(userId, file), Times.Once);
    }

    [Fact]
    public async Task UploadPhoto_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateTestFormFile("test.jpg", "image/jpeg");
        var errorMessage = "Invalid file format";

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.UploadProfilePhotoAsync(userId, file))
            .ReturnsAsync(ServiceResult<PhotoResponse>.CreateFailure(errorMessage));

        // Act
        var result = await _controller.UploadPhoto(file);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<PhotoResponse>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task DeletePhoto_Success_ReturnsOkResponse()
    {
        // Arrange
        var userId = "test-user-id";
        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.DeleteProfilePhotoAsync(userId))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.DeletePhoto();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();

        _mockUserService.Verify(x => x.DeleteProfilePhotoAsync(userId), Times.Once);
    }

    [Fact]
    public async Task DeletePhoto_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var errorMessage = "Photo not found";

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.DeleteProfilePhotoAsync(userId))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.DeletePhoto();

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task AdminUploadPhoto_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var targetUserId = "target-user-id";
        var file = CreateTestFormFile("test.jpg", "image/jpeg");
        var request = new AdminPhotoUploadRequest
        {
            UserId = targetUserId,
            File = file
        };
        var errorMessage = "Invalid file format";

        _mockUserService
            .Setup(x => x.AdminUploadProfilePhotoAsync(targetUserId, file))
            .ReturnsAsync(ServiceResult<PhotoResponse>.CreateFailure(errorMessage));

        // Act
        var result = await _controller.AdminUploadPhoto(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<PhotoResponse>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task AdminUploadPhoto_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var targetUserId = "target-user-id";
        var file = CreateTestFormFile("test.jpg", "image/jpeg");
        var request = new AdminPhotoUploadRequest
        {
            UserId = targetUserId,
            File = file
        };

        var expectedResponse = new PhotoResponse
        {
            PhotoUrl = "https://example.com/photos/test.jpg",
            UpdateDateTime = DateTime.UtcNow
        };

        _mockUserService
            .Setup(x => x.AdminUploadProfilePhotoAsync(targetUserId, file))
            .ReturnsAsync(ServiceResult<PhotoResponse>.CreateSuccess(expectedResponse));

        // Act
        var result = await _controller.AdminUploadPhoto(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<PhotoResponse>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.PhotoUrl.Should().Be(expectedResponse.PhotoUrl);
        response.Errors.Should().BeEmpty();

        _mockUserService.Verify(x => x.AdminUploadProfilePhotoAsync(targetUserId, file), Times.Once);
    }

    [Fact]
    public async Task AdminDeletePhoto_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var targetUserId = "target-user-id";
        var request = new AdminPhotoDeleteRequest { UserId = targetUserId };

        _mockUserService
            .Setup(x => x.AdminDeleteProfilePhotoAsync(targetUserId))
            .ReturnsAsync(ServiceResult.CreateSuccess());

        // Act
        var result = await _controller.AdminDeletePhoto(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();

        _mockUserService.Verify(x => x.AdminDeleteProfilePhotoAsync(targetUserId), Times.Once);
    }

    [Fact]
    public async Task AdminDeletePhoto_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var targetUserId = "target-user-id";
        var request = new AdminPhotoDeleteRequest { UserId = targetUserId };
        var errorMessage = "User not found";

        _mockUserService
            .Setup(x => x.AdminDeleteProfilePhotoAsync(targetUserId))
            .ReturnsAsync(ServiceResult.CreateFailure(errorMessage));

        // Act
        var result = await _controller.AdminDeletePhoto(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().ContainSingle()
            .Which.Message.Should().Be(errorMessage);
    }

    private static IFormFile CreateTestFormFile(string filename, string contentType)
    {
        var content = new byte[] { 0x42, 0x43, 0x44 }; // Dummy file content
        var stream = new MemoryStream(content);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(filename);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.Length).Returns(stream.Length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return file.Object;
    }
}

public class UploadPhotoRequestTests
{
    [Fact]
    public void UploadPhotoRequest_WithValidFile_ShouldValidate()
    {
        // Arrange
        var file = new FormFile(
            baseStream: new MemoryStream(),
            baseStreamOffset: 0,
            length: 1024,
            name: "file",
            fileName: "test.jpg"
        );

        var request = new UploadPhotoRequest { File = file };

        // Act
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }
}
