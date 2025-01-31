using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using System.Net;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
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
                u.NotificationPreference == NotificationPreference.OnlyMyBuySell &&
                u.PositionPreference == PositionPreference.TBD),
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
