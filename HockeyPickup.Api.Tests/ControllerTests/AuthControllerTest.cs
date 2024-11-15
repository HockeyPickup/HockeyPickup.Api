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
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using HockeyPickup.Api.Data.Entities;

namespace HockeyPickup.Api.Tests.Controllers;

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

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = "validPassword123!"
        };

        var user = new User { Id = "123", Username = "user@example.com" };
        var roles = new[] { "User" };
        var resultData = (user, roles);

        var token = "valid.jwt.token";
        var expiration = DateTime.UtcNow.AddHours(1);

        _mockUserService
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync(ServiceResult<(User user, string[] roles)>.CreateSuccess(resultData));

        _mockJwtService
            .Setup(x => x.GenerateToken(user.Id, user.Username, roles))
            .Returns((token, expiration));

        // Act
        var actionResult = await _controller.Login(request);

        // Assert
        actionResult.Should().NotBeNull();
        actionResult.Value.Should().BeOfType<LoginResponse>();
        actionResult.Value.Token.Should().Be(token);
        actionResult.Value.Expiration.Should().Be(expiration);
    }

    [Fact]
    public async Task Login_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "invalid-email",
            Password = "pass"
        };
        _controller.ModelState.AddModelError("Username", "Invalid email format");
        _controller.ModelState.AddModelError("Password", "Password must be at least 8 characters");

        // Act
        var actionResult = await _controller.Login(request);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = actionResult.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().NotBeNull();

        // Verify the error message
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );
        response.Should().NotBeNull();
        response!.Should().ContainKey("message");
        response["message"].Should().Be("Invalid request data");
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = "wrongPassword123!"
        };

        _mockUserService
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync(ServiceResult<(User user, string[] roles)>.CreateFailure("Invalid credentials"));

        // Act
        var actionResult = await _controller.Login(request);

        // Assert
        actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = actionResult.Result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.Value.Should().NotBeNull();

        // Verify the error message
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(unauthorizedResult.Value)
        );
        response.Should().NotBeNull();
        response!.Should().ContainKey("message");
        response["message"].Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Login_UserServiceThrowsException_ThrowsException()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = "password123!"
        };

        _mockUserService
            .Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _controller.Login(request));
    }

    [Fact]
    public async Task Login_EmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "",
            Password = ""
        };
        _controller.ModelState.AddModelError("Username", "Username is required");
        _controller.ModelState.AddModelError("Password", "Password is required");

        // Act
        var actionResult = await _controller.Login(request);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = actionResult.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().NotBeNull();

        // Verify the error message
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );
        response.Should().NotBeNull();
        response!.Should().ContainKey("message");
        response["message"].Should().Be("Invalid request data");
    }
}

public partial class AuthControllerTest
{
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
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult!.Value)
        );
        response!["message"].Should().Be("Logged out successfully");

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
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult!.Value)
        );
        response!["message"].Should().Be("No token found");
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
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult!.Value)
        );
        response!["message"].Should().Be("Token already invalidated");

        // Verify we didn't try to invalidate an already invalid token
        _mockTokenBlacklist.Verify(x => x.InvalidateTokenAsync(It.IsAny<string>()), Times.Never);
    }

    private class AuthorizeActionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            await next();
        }
    }

    [Fact]
    public async Task Logout_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        SetupAuthentication(isAuthenticated: false);

        // Add the authorization filter
        var authorizeFilter = new AuthorizeActionFilter();
        var actionContext = new ActionContext(
            _controller.HttpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            _controller);

        // Act
        await authorizeFilter.OnActionExecutionAsync(
            actionExecutingContext,
            () => Task.FromResult(new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                _controller)));

        // Assert
        actionExecutingContext.Result.Should().BeOfType<UnauthorizedResult>();
    }
}

public partial class AuthControllerTest
{
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
            InviteCode = "VALID-INVITE-123"
        };
    }

    [Fact]
    public async Task Register_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult.CreateSuccess("Registration successful"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be("Registration successful");
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "invalid-email",
            Password = "weak",
            ConfirmPassword = "different",
            FirstName = "Test",
            LastName = "User",
            FrontendUrl = "not-a-url",
            InviteCode = "VALID-INVITE-123"
        };

        _controller.ModelState.AddModelError("Email", "Invalid email format");
        _controller.ModelState.AddModelError("Password", "Password must be at least 8 characters and contain special characters");
        _controller.ModelState.AddModelError("ConfirmPassword", "Passwords don't match");
        _controller.ModelState.AddModelError("FrontendUrl", "Invalid URL format");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid registration data");
        response.Errors.Should().HaveCount(4)
            .And.Contain("Invalid email format")
            .And.Contain("Password must be at least 8 characters and contain special characters")
            .And.Contain("Passwords don't match")
            .And.Contain("Invalid URL format");
    }

    [Fact]
    public async Task Register_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRegisterRequest() with { Email = "existing@example.com" };

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult.CreateFailure("Email already exists"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Registration failed");
        response.Errors.Should().ContainSingle()
            .And.Contain("Email already exists");
    }

    [Fact]
    public async Task Register_ServiceSuccessWithNoMessage_ReturnsDefaultSuccessMessage()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult.CreateSuccess("")); // Empty string for message

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be(""); // Empty string is passed through
        response.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Password must be provided")]
    [InlineData("weak", "Password must contain special characters")]
    [InlineData("Strong1!", "Password must be at least 8 characters")]
    public async Task Register_InvalidPassword_ReturnsBadRequest(string password, string expectedError)
    {
        // Arrange
        var request = CreateValidRegisterRequest() with
        {
            Password = password,
            ConfirmPassword = password
        };

        _controller.ModelState.AddModelError("Password", expectedError);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid registration data");
        response.Errors.Should().Contain(expectedError);
    }

    [Fact]
    public async Task Register_InvalidInviteCode_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRegisterRequest() with { InviteCode = "INVALID-CODE" };

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult.CreateFailure("Invalid invitation code"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Registration failed");
        response.Errors.Should().ContainSingle()
            .And.Contain("Invalid invitation code");
    }

    [Fact]
    public async Task Register_SuccessWithNullMessage_ReturnsDefaultMessage()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        var resultData = (new User { Id = "123", Username = request.Email }, new[] { "User" });

        _mockUserService
            .Setup(x => x.RegisterUserAsync(request))
            .ReturnsAsync(ServiceResult<(User, string[])>.CreateSuccess(resultData, null)); // Null message

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be("Registration successful. Please check your email to confirm your account.");
        response.Errors.Should().BeEmpty();
    }
}

public partial class AuthControllerTest
{
    private static ConfirmEmailRequest CreateValidConfirmEmailRequest()
    {
        return new ConfirmEmailRequest
        {
            Email = "test@example.com",
            Token = "valid-token"
        };
    }

    [Fact]
    public async Task ConfirmEmail_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidConfirmEmailRequest();
        var decodedToken = WebUtility.UrlDecode(request.Token);

        _mockUserService
            .Setup(x => x.ConfirmEmailAsync(request.Email, decodedToken))
            .ReturnsAsync(ServiceResult.CreateSuccess("Email confirmed successfully"));

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ConfirmEmailResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be("Email confirmed successfully");
    }

    [Fact]
    public async Task ConfirmEmail_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new ConfirmEmailRequest
        {
            Email = "invalid-email",
            Token = "valid-token"
        };

        _controller.ModelState.AddModelError("Email", "Invalid email format");

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ConfirmEmailResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid request data");
    }

    [Fact]
    public async Task ConfirmEmail_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidConfirmEmailRequest();
        var decodedToken = WebUtility.UrlDecode(request.Token);

        _mockUserService
            .Setup(x => x.ConfirmEmailAsync(request.Email, decodedToken))
            .ReturnsAsync(ServiceResult.CreateFailure("Invalid or expired token"));

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ConfirmEmailResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid or expired token");
    }

    [Fact]
    public async Task ConfirmEmail_EncodedToken_DecodesCorrectly()
    {
        // Arrange
        var encodedToken = WebUtility.UrlEncode("token+with+special/chars=");
        var request = CreateValidConfirmEmailRequest() with { Token = encodedToken };
        var decodedToken = "token+with+special/chars=";

        _mockUserService
            .Setup(x => x.ConfirmEmailAsync(request.Email, decodedToken))
            .ReturnsAsync(ServiceResult.CreateSuccess("Email confirmed successfully"));

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ConfirmEmailResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be("Email confirmed successfully");

        // Verify the service was called with the decoded token
        _mockUserService.Verify(x => x.ConfirmEmailAsync(request.Email, decodedToken), Times.Once);
    }

    [Theory]
    [InlineData("", "test@example.com", "Token is required")]
    [InlineData("valid-token", "", "Email is required")]
    public async Task ConfirmEmail_MissingRequiredFields_ReturnsBadRequest(
        string token, string email, string expectedError)
    {
        // Arrange
        var request = new ConfirmEmailRequest
        {
            Email = email,
            Token = token
        };

        _controller.ModelState.AddModelError(
            string.IsNullOrEmpty(token) ? "Token" : "Email",
            expectedError);

        // Act
        var result = await _controller.ConfirmEmail(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ConfirmEmailResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid request data");
    }
}

public partial class AuthControllerTest
{
    private static ChangePasswordRequest CreateValidChangePasswordRequest()
    {
        return new ChangePasswordRequest
        {
            CurrentPassword = "OldP@ssword123",
            NewPassword = "NewP@ssword456",
            ConfirmNewPassword = "NewP@ssword456"
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

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.ChangePasswordAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess("Password changed successfully"));

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult.Value)
        );

        response!["message"].Should().Be("Password changed successfully");
    }

    [Fact]
    public async Task ChangePassword_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        SetupUserClaims();
        var request = CreateValidChangePasswordRequest();

        _controller.ModelState.AddModelError("NewPassword", "Password must be at least 8 characters");

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid Request Data");
    }

    [Fact]
    public async Task ChangePassword_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupUserClaims("");  // Empty user ID
        var request = CreateValidChangePasswordRequest();

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(notFoundResult.Value)
        );

        response!["message"].Should().Be("User not found");
    }

    [Fact]
    public async Task ChangePassword_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.ChangePasswordAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateFailure("Current password is incorrect"));

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Current password is incorrect");
    }

    [Fact]
    public async Task ChangePassword_NoAuthenticationClaims_ReturnsNotFound()
    {
        // Arrange
        var request = CreateValidChangePasswordRequest();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(notFoundResult.Value)
        );

        response!["message"].Should().Be("User not found");
    }

    [Fact]
    public async Task ChangePassword_Unauthorized_ReturnsUnauthorizedFromAttribute()
    {
        // Arrange
        var request = CreateValidChangePasswordRequest();

        // Setup empty claims principal to simulate unauthorized user
        var authorizeFilter = new AuthorizeActionFilter();
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            _controller);

        // Act
        await authorizeFilter.OnActionExecutionAsync(
            actionExecutingContext,
            () => Task.FromResult(new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                _controller)));

        // Assert
        actionExecutingContext.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Theory]
    [InlineData("", "NewP@ss123", "NewP@ss123", "Current password is required")]
    [InlineData("CurrentP@ss123", "", "NewP@ss123", "New password is required")]
    [InlineData("CurrentP@ss123", "NewP@ss123", "", "Password confirmation is required")]
    [InlineData("CurrentP@ss123", "NewP@ss123", "DifferentP@ss123", "Password confirmation does not match")]
    public async Task ChangePassword_ValidationErrors_ReturnsBadRequest(
        string currentPassword, string newPassword, string confirmPassword, string expectedError)
    {
        // Arrange
        SetupUserClaims();
        var request = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword,
            ConfirmNewPassword = confirmPassword
        };

        _controller.ModelState.AddModelError("Password", expectedError);

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid Request Data");
    }
}

public partial class AuthControllerTest
{
    private static ForgotPasswordRequest CreateValidForgotPasswordRequest()
    {
        return new ForgotPasswordRequest
        {
            Email = "test@example.com",
            FrontendUrl = "https://example.com/reset-password"
        };
    }

    [Fact]
    public async Task ForgotPassword_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidForgotPasswordRequest();

        _mockUserService
            .Setup(x => x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl))
            .ReturnsAsync(ServiceResult.CreateSuccess("Reset email sent successfully"));

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult.Value)
        );

        response!["message"].Should().Be("If the email exists, a password reset link will be sent");

        // Verify service was called
        _mockUserService.Verify(x =>
            x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_NonexistentEmail_StillReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidForgotPasswordRequest();

        _mockUserService
            .Setup(x => x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl))
            .ReturnsAsync(ServiceResult.CreateFailure("User not found"));

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult.Value)
        );

        // Should return the same message even when email doesn't exist
        response!["message"].Should().Be("If the email exists, a password reset link will be sent");

        // Verify service was still called
        _mockUserService.Verify(x =>
            x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_ServiceError_StillReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidForgotPasswordRequest();

        _mockUserService
            .Setup(x => x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl))
            .ReturnsAsync(ServiceResult.CreateFailure("Error sending email"));

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult.Value)
        );

        // Should still return the same generic message
        response!["message"].Should().Be("If the email exists, a password reset link will be sent");

        // Verify the service was still called
        _mockUserService.Verify(x =>
            x.InitiateForgotPasswordAsync(request.Email, request.FrontendUrl),
            Times.Once);
    }

    [Theory]
    [InlineData("", "https://example.com/reset", "Email is required")]
    [InlineData("test@example.com", "", "Frontend URL is required")]
    [InlineData("invalid-email", "https://example.com/reset", "Invalid email format")]
    [InlineData("test@example.com", "invalid-url", "Invalid URL format")]
    public async Task ForgotPassword_ValidationErrors_ReturnsBadRequest(
        string email, string frontendUrl, string expectedError)
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = email,
            FrontendUrl = frontendUrl
        };

        _controller.ModelState.AddModelError(
            email == "" || !email.Contains("@") ? "Email" : "FrontendUrl",
            expectedError);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid request data");

        // Verify service was never called with invalid data
        _mockUserService.Verify(x =>
            x.InitiateForgotPasswordAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}

public partial class AuthControllerTest
{
    private static ResetPasswordRequest CreateValidResetPasswordRequest()
    {
        return new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "valid-token",
            NewPassword = "NewP@ssword123",
            ConfirmPassword = "NewP@ssword123"
        };
    }

    [Fact]
    public async Task ResetPassword_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var decodedToken = WebUtility.UrlDecode(request.Token);

        _mockUserService
            .Setup(x => x.ResetPasswordAsync(It.Is<ResetPasswordRequest>(r =>
                r.Email == request.Email &&
                r.Token == decodedToken &&
                r.NewPassword == request.NewPassword &&
                r.ConfirmPassword == request.ConfirmPassword)))
            .ReturnsAsync(ServiceResult.CreateSuccess("Password reset successfully"));

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult.Value)
        );

        response!["message"].Should().Be("Password has been reset successfully");
    }

    [Fact]
    public async Task ResetPassword_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        _controller.ModelState.AddModelError("NewPassword", "Password must be at least 8 characters");

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid request data");
    }

    [Fact]
    public async Task ResetPassword_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();

        _mockUserService
            .Setup(x => x.ResetPasswordAsync(It.Is<ResetPasswordRequest>(r =>
                r.Email == request.Email &&
                r.Token == WebUtility.UrlDecode(request.Token))))
            .ReturnsAsync(ServiceResult.CreateFailure("Invalid or expired token"));

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid or expired token");
    }

    [Fact]
    public async Task ResetPassword_EncodedToken_DecodesCorrectly()
    {
        // Arrange
        var encodedToken = WebUtility.UrlEncode("token+with+special/chars=");
        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = encodedToken,
            NewPassword = "NewP@ssword123",
            ConfirmPassword = "NewP@ssword123"
        };
        var decodedToken = "token+with+special/chars=";

        _mockUserService
            .Setup(x => x.ResetPasswordAsync(It.Is<ResetPasswordRequest>(r =>
                r.Token == decodedToken)))
            .ReturnsAsync(ServiceResult.CreateSuccess("Password reset successfully"));

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        _mockUserService.Verify(x => x.ResetPasswordAsync(
            It.Is<ResetPasswordRequest>(r => r.Token == decodedToken)),
            Times.Once);
    }

    [Theory]
    [InlineData("", "validtoken", "NewP@ss123", "NewP@ss123", "Email is required")]
    [InlineData("test@example.com", "", "NewP@ss123", "NewP@ss123", "Token is required")]
    [InlineData("test@example.com", "validtoken", "", "NewP@ss123", "New password is required")]
    [InlineData("test@example.com", "validtoken", "NewP@ss123", "", "Confirm password is required")]
    [InlineData("test@example.com", "validtoken", "NewP@ss123", "Different123!", "Passwords do not match")]
    [InlineData("invalid-email", "validtoken", "NewP@ss123", "NewP@ss123", "Invalid email format")]
    public async Task ResetPassword_ValidationErrors_ReturnsBadRequest(
        string email, string token, string newPassword, string confirmPassword, string expectedError)
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = email,
            Token = token,
            NewPassword = newPassword,
            ConfirmPassword = confirmPassword
        };

        _controller.ModelState.AddModelError("", expectedError);

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid request data");
    }
}

public partial class AuthControllerTest
{
    private static SaveUserRequest CreateValidSaveUserRequest()
    {
        return new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            PayPalEmail = "john.doe@paypal.com",
            VenmoAccount = "johndoe",
            MobileLast4 = "1234",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-123-4567",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            Active = true,
            Preferred = true,
            PreferredPlus = false,
            LockerRoom13 = false
        };
    }

    [Fact]
    public async Task SaveUser_ValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidSaveUserRequest();

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateSuccess("User saved successfully"));

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(okResult.Value)
        );

        response!["message"].Should().Be("User saved successfully");

        // Verify service was called with correct parameters
        _mockUserService.Verify(x => x.SaveUserAsync(userId, request), Times.Once);
    }

    [Fact]
    public async Task SaveUser_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidSaveUserRequest();

        SetupUserClaims(userId);
        _controller.ModelState.AddModelError("PayPalEmail", "Invalid email format");

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid request data");
    }

    [Fact]
    public async Task SaveUser_NoUserClaim_ReturnsNotFound()
    {
        // Arrange
        var request = CreateValidSaveUserRequest();
        SetupUserClaims(""); // Empty user ID

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(notFoundResult.Value)
        );

        response!["message"].Should().Be("User not found");
    }

    [Fact]
    public async Task SaveUser_ServiceFailure_ReturnsBadRequest()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidSaveUserRequest();

        SetupUserClaims(userId);

        _mockUserService
            .Setup(x => x.SaveUserAsync(userId, request))
            .ReturnsAsync(ServiceResult.CreateFailure("Failed to update user"));

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Failed to update user");
    }

    [Theory]
    [InlineData("invalid@paypal.com", "valid-venmo", "1234", "Invalid PayPal email")]
    [InlineData("valid@paypal.com", "inv@lid/venmo", "1234", "Invalid Venmo account")]
    [InlineData("valid@paypal.com", "valid-venmo", "12345", "Invalid mobile number format")]
    public async Task SaveUser_ValidationErrors_ReturnsBadRequest(
        string paypalEmail, string venmoAccount, string mobileLast4, string expectedError)
    {
        // Arrange
        var userId = "test-user-id";
        SetupUserClaims(userId);

        var request = new SaveUserRequest
        {
            PayPalEmail = paypalEmail,
            VenmoAccount = venmoAccount,
            MobileLast4 = mobileLast4
        };

        _controller.ModelState.AddModelError("", expectedError);

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value)
        );

        response!["message"].Should().Be("Invalid request data");
    }

    [Fact]
    public async Task SaveUser_Unauthorized_ReturnsUnauthorizedFromAttribute()
    {
        // Arrange
        var request = CreateValidSaveUserRequest();

        var authorizeFilter = new AuthorizeActionFilter();
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            _controller);

        // Act
        await authorizeFilter.OnActionExecutionAsync(
            actionExecutingContext,
            () => Task.FromResult(new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                _controller)));

        // Assert
        actionExecutingContext.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SaveUser_NoNameIdentifierClaim_ReturnsNotFound()
    {
        // Arrange
        var request = CreateValidSaveUserRequest();

        // Setup claims without NameIdentifier
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "test@example.com")
            // Deliberately omitting NameIdentifier claim
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

        // Act
        var result = await _controller.SaveUser(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(notFoundResult.Value)
        );

        response!["message"].Should().Be("User not found");
    }
}
