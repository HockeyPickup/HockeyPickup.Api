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

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
    private readonly Mock<UserManager<AspNetUser>> _mockUserManager;
    private readonly Mock<SignInManager<AspNetUser>> _mockSignInManager;
    private readonly Mock<IServiceBus> _mockServiceBus;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<UserService>> _mockLogger;
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

        _service = new UserService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockServiceBus.Object,
            _mockConfiguration.Object,
            _mockLogger.Object
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
        result.Data.user.Username.Should().Be(username);
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
        result.Message.Should().Be("An error occurred while validating credentials");

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
    public async Task RegisterUserAsync_EmptyInviteCodeConfig_ReturnsFailure(string configValue)
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
}
