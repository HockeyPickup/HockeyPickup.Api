using Microsoft.Extensions.Logging;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Moq;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
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

    private static UserPaymentMethodResponse CreatePaymentMethodResponse(int id = 1)
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
    public async Task GetUserPaymentMethodsAsync_ValidUser_ReturnsPaymentMethods()
    {
        // Arrange
        var userId = "test-user-id";
        var expectedMethods = new List<UserPaymentMethodResponse>
        {
            CreatePaymentMethodResponse(1),
            CreatePaymentMethodResponse(2)
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodsAsync(userId))
            .ReturnsAsync(expectedMethods);

        // Act
        var result = await _service.GetUserPaymentMethodsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedMethods);
    }

    [Fact]
    public async Task AddUserPaymentMethodAsync_DuplicateMethodType_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreatePaymentMethodRequest(); // Creates PayPal type
        var existingMethods = new List<UserPaymentMethodResponse>
        {
            new()
            {
                UserPaymentMethodId = 1,
                MethodType = PaymentMethodType.PayPal, // Same type as request
                Identifier = "existing@example.com",
                PreferenceOrder = 1,
                IsActive = true
            }
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodsAsync(userId))
            .ReturnsAsync(existingMethods);

        // Act
        var result = await _service.AddUserPaymentMethodAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Payment method type PayPal already exists for this user");

        // Verify we never tried to add the duplicate
        _mockUserRepository.Verify(
            x => x.AddUserPaymentMethodAsync(It.IsAny<string>(), It.IsAny<UserPaymentMethod>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserPaymentMethodsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.GetUserPaymentMethodsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task GetUserPaymentMethodAsync_ValidMethod_ReturnsMethod()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var expectedMethod = CreatePaymentMethodResponse(methodId);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync(expectedMethod);

        // Act
        var result = await _service.GetUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedMethod);
    }

    [Fact]
    public async Task GetUserPaymentMethodAsync_MethodNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 999;

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync((UserPaymentMethodResponse) null!);

        // Act
        var result = await _service.GetUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Payment method not found");
    }

    [Fact]
    public async Task AddUserPaymentMethodAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreatePaymentMethodRequest();
        var expectedResponse = CreatePaymentMethodResponse();

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.AddUserPaymentMethodAsync(userId, It.IsAny<UserPaymentMethod>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.AddUserPaymentMethodAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task UpdateUserPaymentMethodAsync_ValidUpdate_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var request = CreatePaymentMethodRequest();
        var expectedResponse = CreatePaymentMethodResponse(methodId);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.UpdateUserPaymentMethodAsync(userId, It.IsAny<UserPaymentMethod>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.UpdateUserPaymentMethodAsync(userId, methodId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task UpdateUserPaymentMethodAsync_MethodNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 999;
        var request = CreatePaymentMethodRequest();

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.UpdateUserPaymentMethodAsync(userId, It.IsAny<UserPaymentMethod>()))
            .ReturnsAsync((UserPaymentMethodResponse) null!);

        // Act
        var result = await _service.UpdateUserPaymentMethodAsync(userId, methodId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Payment method not found");
    }

    [Fact]
    public async Task DeleteUserPaymentMethodAsync_ValidDelete_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var existingMethod = CreatePaymentMethodResponse(methodId);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync(existingMethod);

        _mockUserRepository.Setup(x => x.DeleteUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(existingMethod);
    }

    [Fact]
    public async Task DeleteUserPaymentMethodAsync_DeleteFails_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var existingMethod = CreatePaymentMethodResponse(methodId);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync(existingMethod);

        _mockUserRepository.Setup(x => x.DeleteUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeleteUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to delete payment method");
    }

    [Fact]
    public async Task GetUserPaymentMethodsAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.GetUserPaymentMethodsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while retrieving payment methods");

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
    public async Task GetUserPaymentMethodAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.GetUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while retrieving payment method");

        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetUserPaymentMethodAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.GetUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task AddUserPaymentMethodAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreatePaymentMethodRequest();
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.AddUserPaymentMethodAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while adding payment method");

        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task AddUserPaymentMethodAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var request = CreatePaymentMethodRequest();

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.AddUserPaymentMethodAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task UpdateUserPaymentMethodAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var request = CreatePaymentMethodRequest();
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.UpdateUserPaymentMethodAsync(userId, methodId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while updating payment method");

        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserPaymentMethodAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var request = CreatePaymentMethodRequest();

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.UpdateUserPaymentMethodAsync(userId, methodId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task DeleteUserPaymentMethodAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;
        var thrownException = new Exception("Database error");

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(thrownException);

        // Act
        var result = await _service.DeleteUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting payment method");

        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => true),
            thrownException,
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUserPaymentMethodAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.DeleteUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task DeleteUserPaymentMethodAsync_PaymentMethodNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var methodId = 1;

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new AspNetUser { Id = userId });

        _mockUserRepository.Setup(x => x.GetUserPaymentMethodAsync(userId, methodId))
            .ReturnsAsync((UserPaymentMethodResponse) null!);

        // Act
        var result = await _service.DeleteUserPaymentMethodAsync(userId, methodId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Payment method not found");
    }
}
