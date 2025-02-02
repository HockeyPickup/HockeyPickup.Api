using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Requests;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
    private static SaveUserRequest CreateSaveUserRequest()
    {
        return new SaveUserRequest
        {
            FirstName = "John",
            LastName = "Doe",
            EmergencyName = "Jane Doe",
            EmergencyPhone = "555-1234",
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            PositionPreference = PositionPreference.TBD,
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
        user.EmergencyName.Should().Be(request.EmergencyName);
        user.EmergencyPhone.Should().Be(request.EmergencyPhone);
        user.NotificationPreference.Should().Be(request.NotificationPreference!.Value);
        user.PositionPreference.Should().Be(request.PositionPreference!.Value);
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
            NotificationPreference = (int) NotificationPreference.None,
            PositionPreference = (int) PositionPreference.TBD
        };

        var request = new SaveUserRequest
        {
            FirstName = "NewFirst",  // Only updating FirstName
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

        // Verify untouched fields
        user.LastName.Should().Be("Name");
        user.NotificationPreference.Should().Be((int) NotificationPreference.None);
        user.PositionPreference.Should().Be((int) PositionPreference.TBD);
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
            NotificationPreference = (int) NotificationPreference.None,
            PositionPreference = (int) PositionPreference.TBD
        };

        var request = new SaveUserRequest
        {
            NotificationPreference = NotificationPreference.None,
            PositionPreference = PositionPreference.TBD
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
        user.PositionPreference.Should().Be((int) PositionPreference.TBD);
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
            NotificationPreference = NotificationPreference.OnlyMyBuySell,
            PositionPreference = PositionPreference.TBD
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
