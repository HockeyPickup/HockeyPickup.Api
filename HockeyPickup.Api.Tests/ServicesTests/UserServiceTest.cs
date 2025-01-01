using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using HockeyPickup.Api.Models.Requests;
using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
    private readonly Mock<UserManager<AspNetUser>> _mockUserManager;
    private readonly Mock<SignInManager<AspNetUser>> _mockSignInManager;
    private readonly Mock<IServiceBus> _mockServiceBus;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;

    private readonly UserService _service;

    public UserServiceTest()
    {
        // UserManager requires these for constructor
        var userStore = new Mock<IUserStore<AspNetUser>>();
        var mockOptions = new Mock<IOptions<IdentityOptions>>();
        var mockPasswordHasher = new Mock<IPasswordHasher<AspNetUser>>();
        var mockUserValidators = new List<IUserValidator<AspNetUser>>();
        var mockPasswordValidators = new List<IPasswordValidator<AspNetUser>>();
        var mockKeyNormalizer = new Mock<ILookupNormalizer>();
        var mockErrors = new Mock<IdentityErrorDescriber>();
        var mockServices = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<UserManager<AspNetUser>>>();

        _mockUserManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object,
            mockOptions.Object,
            mockPasswordHasher.Object,
            mockUserValidators,
            mockPasswordValidators,
            mockKeyNormalizer.Object,
            mockErrors.Object,
            mockServices.Object,
            mockLogger.Object);

        // SignInManager requires these for constructor
        var mockContextAccessor = new Mock<IHttpContextAccessor>();
        var mockClaimsFactory = new Mock<IUserClaimsPrincipalFactory<AspNetUser>>();
        var mockSignInLogger = new Mock<ILogger<SignInManager<AspNetUser>>>();
        var mockAuthenticationSchemeProvider = new Mock<IAuthenticationSchemeProvider>();
        var mockUserConfirmation = new Mock<IUserConfirmation<AspNetUser>>();

        _mockSignInManager = new Mock<SignInManager<AspNetUser>>(
            _mockUserManager.Object,
            mockContextAccessor.Object,
            mockClaimsFactory.Object,
            mockOptions.Object,
            mockSignInLogger.Object,
            mockAuthenticationSchemeProvider.Object,
            mockUserConfirmation.Object);

        _mockServiceBus = new Mock<IServiceBus>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<UserService>>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();

        _service = new UserService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockServiceBus.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockBlobServiceClient.Object
        );
    }

    // Let's start with ValidateCredentialsAsync tests since it's fundamental
    [Fact]
    public async Task ValidateCredentialsAsync_ValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var username = "test@example.com";
        var password = "Password123!";
        var userId = "user-id";
        var roles = new[] { "User" };

        var aspNetUser = new AspNetUser
        {
            Id = userId,
            UserName = username,
            Email = username
        };

        _mockUserManager.Setup(x => x.FindByNameAsync(username))
            .ReturnsAsync(aspNetUser);

        _mockSignInManager
            .Setup(x => x.CheckPasswordSignInAsync(aspNetUser, password, false))
            .ReturnsAsync(SignInResult.Success);

        _mockUserManager.Setup(x => x.IsEmailConfirmedAsync(aspNetUser))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.GetRolesAsync(aspNetUser))
            .ReturnsAsync(roles);

        // Act
        var result = await _service.ValidateCredentialsAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.user.Id.Should().Be(userId);
        result.Data.user.UserName.Should().Be(username);
        result.Data.user.Email.Should().Be(username);
        result.Data.roles.Should().BeEquivalentTo(roles);

        // Verify service bus message was sent
        _mockServiceBus.Verify(x => x.SendAsync(
            It.Is<ServiceBusCommsMessage>(m =>
                m.Metadata["Type"] == "SignedIn" &&
                m.CommunicationMethod["Email"] == username),
            "SignedIn",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()));  // Add the CancellationToken parameter
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var username = "nonexistent@example.com";
        var password = "Password123!";

        _mockUserManager.Setup(x => x.FindByNameAsync(username))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.ValidateCredentialsAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");

        // Verify no service bus message was sent
        _mockServiceBus.Verify(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),  // Add the CancellationToken parameter
            Times.Never);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_InvalidPassword_ReturnsFailure()
    {
        // Arrange
        var username = "test@example.com";
        var password = "WrongPassword123!";
        var aspNetUser = new AspNetUser
        {
            UserName = username,
            Email = username
        };

        _mockUserManager.Setup(x => x.FindByNameAsync(username))
            .ReturnsAsync(aspNetUser);

        _mockSignInManager
            .Setup(x => x.CheckPasswordSignInAsync(aspNetUser, password, false))
            .ReturnsAsync(SignInResult.Failed);

        // Act
        var result = await _service.ValidateCredentialsAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task ValidateCredentialsAsync_EmailNotConfirmed_ReturnsFailure()
    {
        // Arrange
        var username = "test@example.com";
        var password = "Password123!";
        var aspNetUser = new AspNetUser
        {
            UserName = username,
            Email = username
        };

        _mockUserManager.Setup(x => x.FindByNameAsync(username))
            .ReturnsAsync(aspNetUser);

        _mockSignInManager
            .Setup(x => x.CheckPasswordSignInAsync(aspNetUser, password, false))
            .ReturnsAsync(SignInResult.Success);

        _mockUserManager.Setup(x => x.IsEmailConfirmedAsync(aspNetUser))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ValidateCredentialsAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Email not confirmed");
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var username = "test@example.com";
        var password = "Password123!";

        _mockUserManager.Setup(x => x.FindByNameAsync(username))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.ValidateCredentialsAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while validating credentials");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

public partial class UserServiceTest
{
    private static RegisterRequest CreateValidRegisterRequest(string email = "test@example.com")
    {
        return new RegisterRequest
        {
            Email = email,
            Password = "ValidP@ssw0rd",
            ConfirmPassword = "ValidP@ssw0rd",
            FirstName = "Test",
            LastName = "User",
            InviteCode = "valid-code",
            FrontendUrl = "https://example.com"
        };
    }

    [Fact]
    public async Task RegisterUserAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((AspNetUser) null!);

        var createResult = IdentityResult.Success;
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<AspNetUser>(), request.Password))
            .ReturnsAsync(createResult);

        var confirmationToken = "confirmation-token";
        _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<AspNetUser>()))
            .ReturnsAsync(confirmationToken);

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify user creation with correct properties
        _mockUserManager.Verify(x => x.CreateAsync(
            It.Is<AspNetUser>(u =>
                u.Email == request.Email &&
                u.UserName == request.Email &&
                u.FirstName == request.FirstName &&
                u.LastName == request.LastName &&
                u.EmailConfirmed == false &&
                u.LockoutEnabled == false &&
                u.PayPalEmail == request.Email &&
                u.NotificationPreference == (int) NotificationPreference.OnlyMyBuySell),
            request.Password),
            Times.Once);

        // Verify confirmation email was sent
        _mockServiceBus.Verify(x => x.SendAsync(
            It.Is<ServiceBusCommsMessage>(m =>
                m.Metadata["Type"] == "RegisterConfirmation" &&
                m.CommunicationMethod["Email"] == request.Email &&
                m.MessageData["ConfirmationUrl"].Contains(confirmationToken)),
            "RegisterConfirmation",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterUserAsync_InvalidInviteCode_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRegisterRequest() with { InviteCode = "invalid-code" };

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid registration invite code");

        // Verify warning was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            null,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        // Verify user was not created
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<AspNetUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_ExistingConfirmedUser_ReturnsFailure()
    {
        // Arrange
        var email = "existing@example.com";
        var request = CreateValidRegisterRequest(email);

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        var existingUser = new AspNetUser
        {
            Email = email,
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User with this email already exists");

        // Verify user was not created
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<AspNetUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_ExistingUnconfirmedUser_DeletesAndAllowsReregistration()
    {
        // Arrange
        var email = "unconfirmed@example.com";
        var request = CreateValidRegisterRequest(email);

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        var existingUser = new AspNetUser
        {
            Email = email,
            EmailConfirmed = false
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(existingUser);

        _mockUserManager.Setup(x => x.DeleteAsync(existingUser))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<AspNetUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        var confirmationToken = "confirmation-token";
        _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<AspNetUser>()))
            .ReturnsAsync(confirmationToken);

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify old user was deleted
        _mockUserManager.Verify(x => x.DeleteAsync(existingUser), Times.Once);

        // Verify new user was created
        _mockUserManager.Verify(x => x.CreateAsync(
            It.Is<AspNetUser>(u => u.Email == request.Email),
            request.Password),
            Times.Once);
    }

    [Fact]
    public async Task RegisterUserAsync_UserCreationFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((AspNetUser) null!);

        var errors = new[]
        {
            new IdentityError { Description = "Password too weak" },
            new IdentityError { Description = "Email invalid" }
        };
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<AspNetUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Password too weak, Email invalid");
    }

    [Fact]
    public async Task RegisterUserAsync_ServiceBusFailure_ReturnsSuccessWithWarning()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((AspNetUser) null!);

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<AspNetUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service bus error"));

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Registration successful but confirmation email could not be sent");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RegisterUserAsync_EmptyInviteCodeConfig_ReturnsFailure(string? configValue)
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns(configValue);

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid registration invite code");

        // Verify warning was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            null,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        // Verify user was not created
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<AspNetUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_TopLevelException_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        _mockConfiguration.Setup(x => x["RegistrationInviteCode"])
            .Returns("valid-code");

        // Force a top-level exception by making UserManager throw
        var thrownException = new InvalidOperationException("Database connection failed");
        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.RegisterUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred during registration");

        // Verify error was logged with correct exception and message
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => string.Equals(o.ToString(),
                $"Error registering user with email {request.Email}")),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task ConfirmEmailAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var token = "valid-token";

        var user = new AspNetUser
        {
            Email = email,
            EmailConfirmed = false
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ConfirmEmailAsync(email, token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Email confirmed successfully. You can now log in.");

        _mockUserManager.Verify(x => x.ConfirmEmailAsync(user, token), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmailAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var token = "valid-token";

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.ConfirmEmailAsync(email, token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid verification link");

        // Verify confirmation was never attempted
        _mockUserManager.Verify(x => x.ConfirmEmailAsync(It.IsAny<AspNetUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmailAsync_AlreadyConfirmed_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var token = "valid-token";

        var user = new AspNetUser
        {
            Email = email,
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _service.ConfirmEmailAsync(email, token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Email is already confirmed");

        // Verify confirmation was never attempted
        _mockUserManager.Verify(x => x.ConfirmEmailAsync(It.IsAny<AspNetUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmailAsync_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var token = "invalid-token";

        var user = new AspNetUser
        {
            Email = email,
            EmailConfirmed = false
        };

        var errors = new[]
        {
            new IdentityError { Description = "Invalid token" }
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ConfirmEmailAsync(email, token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Email confirmation failed. The link may have expired.");

        // Verify warning was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            null,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmEmailAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var token = "valid-token";
        var thrownException = new InvalidOperationException("Database error");

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.ConfirmEmailAsync(email, token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while confirming email");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

public partial class UserServiceTest
{
    private static ChangePasswordRequest CreateValidChangePasswordRequest()
    {
        return new ChangePasswordRequest
        {
            CurrentPassword = "OldP@ssword123",
            NewPassword = "NewP@ssword123",
            ConfirmNewPassword = "NewP@ssword123"
        };
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all operations were performed
        _mockUserManager.Verify(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword), Times.Once);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";
        var request = CreateValidChangePasswordRequest();

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");

        // Verify no password operations were performed
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<AspNetUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<AspNetUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_IncorrectCurrentPassword_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Current password is incorrect");

        // Verify no password change was attempted
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<AspNetUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_NewPasswordSameAsCurrent_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var currentPassword = "CurrentP@ssword123";
        var request = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = currentPassword,
            ConfirmNewPassword = currentPassword
        };

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("New password must be different from current password");

        // Verify no password change was attempted
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<AspNetUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("simple")]  // Too simple
    [InlineData("onlylowercase123")]  // No uppercase
    [InlineData("ONLYUPPERCASE123")]  // No lowercase
    [InlineData("NoSpecialChars123")] // No special characters
    [InlineData("No@Numbers")]        // No numbers
    public async Task ChangePasswordAsync_InvalidPasswordComplexity_ReturnsFailure(string newPassword)
    {
        // Arrange
        var userId = "test-user-id";
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldP@ssword123",
            NewPassword = newPassword,
            ConfirmNewPassword = newPassword
        };

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Password does not meet complexity requirements");

        // Verify no password change was attempted
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<AspNetUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_IdentityChangeFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        var errors = new[]
        {
            new IdentityError { Description = "Password change failed" }
        };
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Password change failed");
    }

    [Fact]
    public async Task ChangePasswordAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();
        var thrownException = new InvalidOperationException("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while changing the password");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_IdentityErrorWithoutDescription_ReturnsGenericFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        var errors = new[]
        {
            new IdentityError { Description = null! }  // Null description
        };
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to change password");  // Default message when description is null
    }

    [Fact]
    public async Task ChangePasswordAsync_IdentityFailureWithNoErrors_ReturnsGenericFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed());  // Empty errors collection

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to change password");
    }

    [Fact]
    public async Task ChangePasswordAsync_IdentityFailureWithNullError_ReturnsGenericFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        var errors = new IdentityError[] { null! };  // Null error object
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to change password");
    }

    [Fact]
    public async Task ChangePasswordAsync_IdentityFailureWithNullDescription_ReturnsGenericFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        var errors = new[]
        {
            new IdentityError { Description = null! }  // Error with null description
        };
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to change password");
    }

    [Fact]
    public async Task ChangePasswordAsync_IdentityFailureWithDescription_ReturnsErrorMessage()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateValidChangePasswordRequest();

        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, request.CurrentPassword))
            .ReturnsAsync(true);

        var errors = new[]
        {
            new IdentityError { Description = "Specific error message" }
        };
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ChangePasswordAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Specific error message");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task InitiateForgotPasswordAsync_ValidEmail_ReturnsSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var frontendUrl = "https://example.com";
        var userId = "test-user-id";
        var firstName = "Test";
        var lastName = "User";
        var resetToken = "password-reset-token";

        var user = new AspNetUser
        {
            Id = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync(resetToken);

        // Act
        var result = await _service.InitiateForgotPasswordAsync(email, frontendUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify service bus message was sent with correct data
        _mockServiceBus.Verify(x => x.SendAsync(
            It.Is<ServiceBusCommsMessage>(m =>
                m.Metadata["Type"] == "ForgotPassword" &&
                m.CommunicationMethod["Email"] == email &&
                m.RelatedEntities["UserId"] == userId &&
                m.RelatedEntities["FirstName"] == firstName &&
                m.RelatedEntities["LastName"] == lastName &&
                m.MessageData["ResetUrl"].Contains(WebUtility.UrlEncode(resetToken)) &&
                m.MessageData["ResetUrl"].Contains(WebUtility.UrlEncode(email)) &&
                m.MessageData["ResetUrl"].StartsWith(frontendUrl.TrimEnd('/'))),
            "ForgotPassword",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitiateForgotPasswordAsync_UserNotFound_ReturnsSuccessForSecurity()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var frontendUrl = "https://example.com";

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.InitiateForgotPasswordAsync(email, frontendUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();  // Should still return success for security

        // Verify no service bus message was sent
        _mockServiceBus.Verify(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InitiateForgotPasswordAsync_ServiceBusFailure_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var frontendUrl = "https://example.com";
        var userId = "test-user-id";
        var resetToken = "password-reset-token";

        var user = new AspNetUser
        {
            Id = userId,
            Email = email
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync(resetToken);

        var serviceBusException = new Exception("Service bus error");
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(serviceBusException);

        // Act
        var result = await _service.InitiateForgotPasswordAsync(email, frontendUrl);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while initiating forgot password");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            serviceBusException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task InitiateForgotPasswordAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var email = "test@example.com";
        var frontendUrl = "https://example.com";
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.InitiateForgotPasswordAsync(email, frontendUrl);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while initiating forgot password");

        // Verify error was logged with correct content
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Theory]
    [InlineData("https://example.com/")]     // With trailing slash
    [InlineData("https://example.com")]      // Without trailing slash
    public async Task InitiateForgotPasswordAsync_DifferentUrlFormats_HandlesCorrectly(string frontendUrl)
    {
        // Arrange
        var email = "test@example.com";
        var resetToken = "password-reset-token";
        var user = new AspNetUser { Email = email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync(resetToken);

        string capturedUrl = null!;
        _mockServiceBus.Setup(x => x.SendAsync(
            It.IsAny<ServiceBusCommsMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<ServiceBusCommsMessage, string, string, string, CancellationToken>(
                (message, _, _, _, _) => capturedUrl = message.MessageData["ResetUrl"])
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.InitiateForgotPasswordAsync(email, frontendUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify URL format
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().StartWith(frontendUrl.TrimEnd('/'));
        capturedUrl.Should().Contain("/reset-password");
        capturedUrl.Should().Contain($"token={WebUtility.UrlEncode(resetToken)}");
        capturedUrl.Should().Contain($"email={WebUtility.UrlEncode(email)}");
    }
}

public partial class UserServiceTest
{
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

    [Fact]
    public async Task ResetPasswordAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var user = new AspNetUser
        {
            Email = request.Email,
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, request.Token, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify password was reset
        _mockUserManager.Verify(x => x.ResetPasswordAsync(user, request.Token, request.NewPassword), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid reset attempt");

        // Verify reset was never attempted
        _mockUserManager.Verify(x => x.ResetPasswordAsync(It.IsAny<AspNetUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("simple")]  // Too simple
    [InlineData("onlylowercase123")]  // No uppercase
    [InlineData("ONLYUPPERCASE123")]  // No lowercase
    [InlineData("NoSpecialChars123")] // No special characters
    [InlineData("No@Numbers")]        // No numbers
    public async Task ResetPasswordAsync_InvalidPasswordComplexity_ReturnsFailure(string newPassword)
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "valid-reset-token",
            NewPassword = newPassword,
            ConfirmPassword = newPassword
        };

        var user = new AspNetUser { Email = request.Email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Password does not meet complexity requirements");

        // Verify reset was never attempted
        _mockUserManager.Verify(x => x.ResetPasswordAsync(It.IsAny<AspNetUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var user = new AspNetUser { Email = request.Email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        var errors = new[] { new IdentityError { Description = "Invalid token" } };
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, request.Token, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid token");
    }

    [Fact]
    public async Task ResetPasswordAsync_UnconfirmedEmail_ConfirmsEmailOnSuccess()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var user = new AspNetUser
        {
            Email = request.Email,
            EmailConfirmed = false  // Unconfirmed email
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, request.Token, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();  // Should be set to true

        // Verify user was updated
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_AlreadyConfirmedEmail_DoesNotUpdateUser()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var user = new AspNetUser
        {
            Email = request.Email,
            EmailConfirmed = true  // Already confirmed
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, request.Token, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify user was not updated
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<AspNetUser>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while resetting the password");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

public partial class UserServiceTest
{
    private static SaveUserRequest CreateSaveUserRequest()
    {
        return new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            PayPalEmail = "john.pay@example.com",
            VenmoAccount = "johndoe",
            MobileLast4 = "1234",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-1234",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
        };
    }

    [Fact]
    public async Task SaveUserAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateSaveUserRequest();
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all properties were updated
        user.FirstName.Should().Be(request.FirstName);
        user.LastName.Should().Be(request.LastName);
        user.PayPalEmail.Should().Be(request.PayPalEmail);
        user.VenmoAccount.Should().Be(request.VenmoAccount);
        user.MobileLast4.Should().Be(request.MobileLast4);
        user.EmergencyName.Should().Be(request.EmergencyName);
        user.EmergencyPhone.Should().Be(request.EmergencyPhone);
        user.NotificationPreference.Should().Be((int) request.NotificationPreference!.Value);
    }

    [Fact]
    public async Task SaveUserAsync_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new AspNetUser
        {
            Id = userId,
            FirstName = "Original",
            LastName = "Name",
            PayPalEmail = "original@pay.com",
            NotificationPreference = (int) NotificationPreference.None
        };

        var request = new SaveUserRequest
        {
            FirstName = "NewFirst",  // Only updating FirstName
            PayPalEmail = "new@pay.com"  // And PayPalEmail
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify updated fields
        user.FirstName.Should().Be("NewFirst");
        user.PayPalEmail.Should().Be("new@pay.com");

        // Verify untouched fields
        user.LastName.Should().Be("Name");
        user.NotificationPreference.Should().Be((int) NotificationPreference.None);
    }

    [Fact]
    public async Task SaveUserAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";
        var request = CreateSaveUserRequest();

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");

        // Verify update was never attempted
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<AspNetUser>()), Times.Never);
    }

    [Fact]
    public async Task SaveUserAsync_UpdateFails_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateSaveUserRequest();
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var errors = new[] { new IdentityError { Description = "Update failed" } };
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task SaveUserAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateSaveUserRequest();
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while saving user");

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task SaveUserAsync_NullableEnumUpdate_HandlesCorrectly()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new AspNetUser
        {
            Id = userId,
            NotificationPreference = (int) NotificationPreference.None
        };

        var request = new SaveUserRequest
        {
            NotificationPreference = NotificationPreference.None
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.NotificationPreference.Should().Be((int) NotificationPreference.None);
    }

    [Fact]
    public async Task SaveUserAsync_UpdateFailsWithEmptyErrors_ReturnsGenericFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreateSaveUserRequest();
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(Array.Empty<IdentityError>()));  // Empty error array

        // Act
        var result = await _service.SaveUserAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to save user");  // Default message when no errors
    }

    [Fact]
    public async Task ResetPasswordAsync_FailureWithEmptyErrors_ReturnsGenericFailure()
    {
        // Arrange
        var request = CreateValidResetPasswordRequest();
        var user = new AspNetUser { Email = request.Email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, request.Token, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(Array.Empty<IdentityError>()));  // Empty error array

        // Act
        var result = await _service.ResetPasswordAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to reset password");  // Default message when no errors
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task GetUserByIdAsync_ValidId_ReturnsUser()
    {
        // Arrange
        var userId = "test-user-id";
        var expectedUser = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserByIdAsync_InvalidId_ReturnsNull()
    {
        // Arrange
        var userId = "nonexistent-id";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByIdAsync_Exception_ReturnsNull()
    {
        // Arrange
        var userId = "test-user-id";
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().BeNull();

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetUserRolesAsync_ValidUser_ReturnsRoles()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id" };
        var expectedRoles = new[] { "Admin", "User" };

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(expectedRoles);

        // Act
        var result = await _service.GetUserRolesAsync(user);

        // Assert
        result.Should().BeEquivalentTo(expectedRoles);
    }

    [Fact]
    public async Task GetUserRolesAsync_NoRoles_ReturnsEmptyArray()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id" };

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _service.GetUserRolesAsync(user);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_Exception_ReturnsEmptyArray()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id" };
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.GetUserRolesAsync(user);

        // Assert
        result.Should().BeEmpty();

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task IsInRoleAsync_UserInRole_ReturnsTrue()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id" };
        var role = "Admin";

        _mockUserManager.Setup(x => x.IsInRoleAsync(user, role))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsInRoleAsync(user, role);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInRoleAsync_UserNotInRole_ReturnsFalse()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id" };
        var role = "Admin";

        _mockUserManager.Setup(x => x.IsInRoleAsync(user, role))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsInRoleAsync(user, role);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsInRoleAsync_Exception_ReturnsFalse()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id" };
        var role = "Admin";
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.IsInRoleAsync(user, role))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.IsInRoleAsync(user, role);

        // Assert
        result.Should().BeFalse();

        // Verify error was logged
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

public partial class UserServiceTest
{
    private static AdminUserUpdateRequest CreateValidAdminUpdateRequest()
    {
        return new AdminUserUpdateRequest
        {
            UserId = "test-user-id",
            FirstName = "John",
            LastName = "Doe",
            Rating = 4.5m,
            Active = true,
            Preferred = true,
            PreferredPlus = false,
            LockerRoom13 = false,
            PayPalEmail = "john.pay@example.com",
            NotificationPreference = NotificationPreference.OnlyMyBuySell
        };
    }

    [Fact]
    public async Task AdminUpdateUserAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = CreateValidAdminUpdateRequest();
        var user = new AspNetUser { Id = request.UserId };

        _mockUserManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AdminUpdateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all properties were updated correctly
        user.FirstName.Should().Be(request.FirstName);
        user.LastName.Should().Be(request.LastName);
        user.Rating.Should().Be(request.Rating!.Value);
        user.Active.Should().Be(request.Active!.Value);
        user.Preferred.Should().Be(request.Preferred!.Value);
        user.PreferredPlus.Should().Be(request.PreferredPlus!.Value);
        user.LockerRoom13.Should().Be(request.LockerRoom13!.Value);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidAdminUpdateRequest();

        _mockUserManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.AdminUpdateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task AdminUpdateUserAsync_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var user = new AspNetUser
        {
            Id = "test-user-id",
            FirstName = "Original",
            LastName = "Name",
            Rating = 3.0m,
            Active = false
        };

        var request = new AdminUserUpdateRequest
        {
            UserId = user.Id,
            FirstName = "NewFirst",  // Only updating FirstName
            Rating = 4.0m           // And Rating
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AdminUpdateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify updated fields
        user.FirstName.Should().Be("NewFirst");
        user.Rating.Should().Be(4.0m);

        // Verify untouched fields
        user.LastName.Should().Be("Name");
        user.Active.Should().BeFalse();
    }

    [Fact]
    public async Task AdminUpdateUserAsync_UpdateFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidAdminUpdateRequest();
        var user = new AspNetUser { Id = request.UserId };

        _mockUserManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);

        var errors = new[] { new IdentityError { Description = "Update failed" } };
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _service.AdminUpdateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task AdminUpdateUserAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidAdminUpdateRequest();
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.AdminUpdateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while updating user");

        // Verify error was logged with correct message and exception
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUserAsync_UpdateFailsWithNoErrors_ReturnsGenericFailure()
    {
        // Arrange
        var request = CreateValidAdminUpdateRequest();
        var user = new AspNetUser { Id = request.UserId };

        _mockUserManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);

        // Setup update to fail but return no errors
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed());  // Empty errors collection

        // Act
        var result = await _service.AdminUpdateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update user");  // Tests the default error message
    }
}

public partial class UserServiceTest
{
    private IFormFile CreateMockFormFile(string fileName, string contentType, long length)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return file.Object;
    }

    private Mock<BlobContainerClient> SetupMockBlobContainer(string blobUri = "https://storage.test/container/test-blob")
    {
        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response>();

        // Setup blob client
        mockBlobClient.Setup(b => b.Uri).Returns(new Uri(blobUri));

        // Setup blob upload
        var mockBlobContentInfo = new Mock<BlobContentInfo>();
        var mockBlobContentResponse = new Mock<Response<BlobContentInfo>>();
        mockBlobContentResponse.Setup(r => r.Value).Returns(mockBlobContentInfo.Object);
        mockBlobContentResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(mockBlobContentResponse.Object));

        // Setup blob delete
        var mockDeleteResponse = new Mock<Response<bool>>();
        mockDeleteResponse.Setup(r => r.Value).Returns(true);
        mockDeleteResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.None,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(mockDeleteResponse.Object));

        // Setup container
        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        var mockBlobContainerInfo = new Mock<BlobContainerInfo>();
        var mockContainerResponse = new Mock<Response<BlobContainerInfo>>();
        mockContainerResponse.Setup(r => r.Value).Returns(mockBlobContainerInfo.Object);
        mockContainerResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockContainerClient.Setup(c => c.CreateIfNotExistsAsync(
            PublicAccessType.Blob,
            It.IsAny<IDictionary<string, string>>(),
            default,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContainerResponse.Object);

        var mockAccessPolicyResponse = new Mock<Response<BlobContainerInfo>>();
        mockAccessPolicyResponse.Setup(r => r.Value).Returns(mockBlobContainerInfo.Object);
        mockAccessPolicyResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockContainerClient.Setup(c => c.SetAccessPolicyAsync(
            PublicAccessType.Blob,
            It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAccessPolicyResponse.Object);

        return mockContainerClient;
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ValidFile_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
        user.PhotoUrl.Should().Be(blobUri);
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Theory]
    [InlineData("photo.txt")]
    [InlineData("photo.gif")]
    [InlineData("photo.bmp")]
    public async Task UploadProfilePhotoAsync_InvalidFileType_ReturnsFailure(string fileName)
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile(fileName, "image/gif", 1024 * 1024);
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid file type. Only JPG and PNG files are allowed");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task DeleteProfilePhotoAsync_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = SetupMockBlobContainer(photoUrl);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_NoPhotoExists_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new AspNetUser { Id = userId, PhotoUrl = null };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("No photo to delete");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_BlobStorageError_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.None,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Blob storage error"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting the photo");
    }

    [Fact]
    public async Task AdminUploadProfilePhotoAsync_ValidFile_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AdminUploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
    }

    [Fact]
    public async Task AdminDeleteProfilePhotoAsync_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = SetupMockBlobContainer(photoUrl);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AdminDeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PhotoUrl.Should().BeNull();
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task UploadProfilePhotoAsync_NullFile_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("No file uploaded");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_BlobUploadFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response>();

        mockBlobClient.Setup(b => b.Uri).Returns(new Uri("https://test.com/blob"));
        mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Upload failed"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while processing the photo");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_BlobDeleteFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            It.IsAny<DeleteSnapshotsOption>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Failed to delete blob"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting the photo");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_UserUpdateFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Failed to update user" } }));

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update user");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task UploadProfilePhotoAsync_SecurityStampUpdateFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response>();

        // Setup container initialization
        var mockBlobContainerInfo = new Mock<BlobContainerInfo>();
        var mockContainerResponse = new Mock<Response<BlobContainerInfo>>();
        mockContainerResponse.Setup(r => r.Value).Returns(mockBlobContainerInfo.Object);
        mockContainerResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockContainerClient.Setup(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContainerResponse.Object);

        mockContainerClient.Setup(c => c.SetAccessPolicyAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Setup blob client
        mockBlobClient.Setup(b => b.Uri).Returns(new Uri("https://test.com/newblob"));
        var mockBlobContentInfo = new Mock<BlobContentInfo>();
        var mockBlobContentResponse = new Mock<Response<BlobContentInfo>>();
        mockBlobContentResponse.Setup(r => r.Value).Returns(mockBlobContentInfo.Object);
        mockBlobContentResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobContentResponse.Object);

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Failed to update security stamp" } }));

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update security stamp");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_OldPhotoDeleteFailure_ContinuesProcessing()
    {
        // Arrange
        var userId = "test-user-id";
        var oldPhotoUrl = "https://storage.test/container/old-photo.jpg";
        var newBlobUri = "https://storage.test/container/new-photo.jpg";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId, PhotoUrl = oldPhotoUrl };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockOldBlobClient = new Mock<BlobClient>();
        var mockNewBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response>();

        // Setup container initialization
        var mockBlobContainerInfo = new Mock<BlobContainerInfo>();
        var mockContainerResponse = new Mock<Response<BlobContainerInfo>>();
        mockContainerResponse.Setup(r => r.Value).Returns(mockBlobContainerInfo.Object);
        mockContainerResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockContainerClient.Setup(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContainerResponse.Object);

        mockContainerClient.Setup(c => c.SetAccessPolicyAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Setup old blob to fail deletion
        mockOldBlobClient.Setup(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.None,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Delete failed"));

        // Setup new blob for successful upload
        mockNewBlobClient.Setup(b => b.Uri).Returns(new Uri(newBlobUri));
        var mockBlobContentInfo = new Mock<BlobContentInfo>();
        var mockBlobContentResponse = new Mock<Response<BlobContentInfo>>();
        mockBlobContentResponse.Setup(r => r.Value).Returns(mockBlobContentInfo.Object);
        mockBlobContentResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockNewBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobContentResponse.Object);

        mockContainerClient.Setup(c => c.GetBlobClient("old-photo.jpg"))
            .Returns(mockOldBlobClient.Object);
        mockContainerClient.Setup(c => c.GetBlobClient(It.Is<string>(s => s.Contains(userId))))
            .Returns(mockNewBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(newBlobUri);
    }
}
