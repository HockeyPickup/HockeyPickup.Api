using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq.Expressions;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class RegularServiceTests
{
    private readonly Mock<UserManager<AspNetUser>> _userManager;
    private readonly Mock<IRegularRepository> _mockRegularRepository;
    private readonly Mock<IServiceBus> _serviceBus;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly RegularService _regularService;

    public RegularServiceTests()
    {
        // Setup UserManager mock with proper dependencies
        var userStore = new Mock<IUserStore<AspNetUser>>();
        _userManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<AspNetUser>>(),
            Array.Empty<IUserValidator<AspNetUser>>(),
            Array.Empty<IPasswordValidator<AspNetUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<AspNetUser>>>());

        _mockRegularRepository = new Mock<IRegularRepository>();
        _serviceBus = new Mock<IServiceBus>();
        _configuration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<UserService>>();

        _regularService = new RegularService(
            _userManager.Object,
            _mockRegularRepository.Object,
            _serviceBus.Object,
            _configuration.Object,
            _mockLogger.Object);
    }

    private static RegularSetDetailedResponse CreateTestRegularSet()
    {
        return new RegularSetDetailedResponse
        {
            RegularSetId = 1,
            Description = "Test Regular Set",
            DayOfWeek = 1,
            CreateDateTime = DateTime.UtcNow,
            Archived = false,
            Regulars = new List<RegularDetailedResponse>()
        };
    }

    [Fact]
    public async Task DuplicateRegularSet_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        var sourceSet = CreateTestRegularSet();
        var newSet = CreateTestRegularSet();
        newSet.RegularSetId = 2;
        newSet.Description = request.Description;

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(sourceSet);
        _mockRegularRepository.Setup(x => x.DuplicateRegularSetAsync(
            request.RegularSetId, request.Description))
            .ReturnsAsync(newSet);

        // Act
        var result = await _regularService.DuplicateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.RegularSetId.Should().Be(2);
        result.Data.Description.Should().Be(request.Description);

        _mockRegularRepository.Verify(x => x.DuplicateRegularSetAsync(
            request.RegularSetId, request.Description), Times.Once);
    }

    [Fact]
    public async Task DuplicateRegularSet_SourceSetNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        // Act
        var result = await _regularService.DuplicateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be($"Regular set with Id {request.RegularSetId} not found");
        _mockRegularRepository.Verify(x => x.DuplicateRegularSetAsync(
            It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateRegularSet_EmptyDescription_ReturnsFailure()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = ""
        };

        // Act
        var result = await _regularService.DuplicateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Description is required");
        _mockRegularRepository.Verify(x => x.DuplicateRegularSetAsync(
            It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateRegularSet_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        var sourceSet = CreateTestRegularSet();
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(sourceSet);
        _mockRegularRepository.Setup(x => x.DuplicateRegularSetAsync(
            request.RegularSetId, request.Description))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.DuplicateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Test exception");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task DuplicateRegularSet_FailedToCreateNewSet_ReturnsFailure()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Test Set"
        };

        var sourceSet = new RegularSetDetailedResponse
        {
            RegularSetId = request.RegularSetId,
            Description = "Original Set",
            DayOfWeek = 1,
            CreateDateTime = DateTime.UtcNow,
            Archived = false,
        };

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(sourceSet);

        _mockRegularRepository.Setup(x => x.DuplicateRegularSetAsync(
            request.RegularSetId, request.Description))
            .ReturnsAsync((RegularSetDetailedResponse) null);

        // Act
        var result = await _regularService.DuplicateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to create new regular set");
    }

    [Fact]
    public async Task UpdateRegularSet_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Updated Regular Set",
            DayOfWeek = 2,
            Archived = true
        };

        var updatedSet = CreateTestRegularSet();
        updatedSet.Description = request.Description;
        updatedSet.DayOfWeek = request.DayOfWeek;
        updatedSet.Archived = request.Archived;

        _mockRegularRepository.Setup(x => x.UpdateRegularSetAsync(
            request.RegularSetId, request.Description, request.DayOfWeek, request.Archived))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.UpdateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Description.Should().Be(request.Description);
        result.Data.DayOfWeek.Should().Be(request.DayOfWeek);
        result.Data.Archived.Should().Be(request.Archived);

        _mockRegularRepository.Verify(x => x.UpdateRegularSetAsync(
            request.RegularSetId, request.Description, request.DayOfWeek, request.Archived),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularSet_EmptyDescription_ReturnsFailure()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "",
            DayOfWeek = 2,
            Archived = false
        };

        // Act
        var result = await _regularService.UpdateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Description is required");
        _mockRegularRepository.Verify(x => x.UpdateRegularSetAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateRegularSet_InvalidDayOfWeek_ReturnsFailure()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 7,
            Archived = false
        };

        // Act
        var result = await _regularService.UpdateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Day of week must be between 0 and 6");
        _mockRegularRepository.Verify(x => x.UpdateRegularSetAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateRegularSet_UpdateFailed_ReturnsFailure()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 2,
            Archived = false
        };

        _mockRegularRepository.Setup(x => x.UpdateRegularSetAsync(
            request.RegularSetId, request.Description, request.DayOfWeek, request.Archived))
            .ReturnsAsync((RegularSetDetailedResponse) null);

        // Act
        var result = await _regularService.UpdateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be($"Failed to update regular set with Id {request.RegularSetId}");
    }

    [Fact]
    public async Task UpdateRegularSet_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Test Set",
            DayOfWeek = 2,
            Archived = false
        };

        _mockRegularRepository.Setup(x => x.UpdateRegularSetAsync(
            request.RegularSetId, request.Description, request.DayOfWeek, request.Archived))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.UpdateRegularSet(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Test exception");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularPosition_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user";
        var regularSetId = 1;
        var newPosition = 1;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new() {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = 2,
                TeamAssignment = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        var updatedSet = CreateTestRegularSet();
        updatedSet.Regulars = new List<RegularDetailedResponse>
        {
            new() {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = newPosition,
                TeamAssignment = 1
            }
        };

        _mockRegularRepository.Setup(x => x.UpdatePlayerPositionAsync(regularSetId, userId, newPosition))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Message.Should().Contain($"{user.FirstName} {user.LastName}'s position preference updated to");
    }

    [Fact]
    public async Task UpdateRegularTeam_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user";
        var regularSetId = 1;
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new() {
                UserId = userId,
                RegularSetId = regularSetId,
                TeamAssignment = 1,
                PositionPreference = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        var updatedSet = CreateTestRegularSet();
        updatedSet.Regulars = new List<RegularDetailedResponse>
        {
            new() {
                UserId = userId,
                RegularSetId = regularSetId,
                TeamAssignment = newTeam,
                PositionPreference = 1
            }
        };

        _mockRegularRepository.Setup(x => x.UpdatePlayerTeamAsync(regularSetId, userId, newTeam))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Message.Should().Contain($"{user.FirstName} {user.LastName}'s team assignment updated to");
    }

    [Fact]
    public async Task UpdateRegularPosition_UserNotInRegularSet_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user";
        var regularSetId = 1;
        var newPosition = 1;

        var user = new AspNetUser { Id = userId };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new() {
                UserId = "different-user",
                RegularSetId = regularSetId,
                PositionPreference = 1,
                TeamAssignment = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
        _mockRegularRepository.Verify(x => x.UpdatePlayerPositionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRegularTeam_UserNotInRegularSet_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user";
        var regularSetId = 1;
        var newTeam = 2;

        var user = new AspNetUser { Id = userId };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new() {
                UserId = "different-user",
                RegularSetId = regularSetId,
                TeamAssignment = 1,
                PositionPreference = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
        _mockRegularRepository.Verify(x => x.UpdatePlayerTeamAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRegularPosition_SamePosition_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var currentPosition = 1;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
    {
        new()
        {
            UserId = userId,
            RegularSetId = regularSetId,
            PositionPreference = currentPosition,
            TeamAssignment = 1
        }
    };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, currentPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("New position is the same as the current position");
    }

    [Fact]
    public async Task UpdateRegularPosition_FailedToUpdate_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = 1,
                TeamAssignment = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.UpdatePlayerPositionAsync(regularSetId, userId, newPosition))
            .ReturnsAsync((RegularSetDetailedResponse) null);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update player position");
    }

    [Fact]
    public async Task UpdateRegularPosition_ThrowsException_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
    {
        new()
        {
            UserId = userId,
            RegularSetId = regularSetId,
            PositionPreference = 1,
            TeamAssignment = 1
        }
    };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.UpdatePlayerPositionAsync(regularSetId, userId, newPosition))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().StartWith("An error occurred updating player position");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularTeam_SameTeam_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var currentTeam = 1;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                TeamAssignment = currentTeam,
                PositionPreference = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, currentTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("New team assignment is the same as the current team assignment");
    }

    [Fact]
    public async Task UpdateRegularTeam_FailedToUpdate_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                TeamAssignment = 1,
                PositionPreference = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.UpdatePlayerTeamAsync(regularSetId, userId, newTeam))
            .ReturnsAsync((RegularSetDetailedResponse) null);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update player team");
    }

    [Fact]
    public async Task UpdateRegularTeam_ThrowsException_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                TeamAssignment = 1,
                PositionPreference = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.UpdatePlayerTeamAsync(regularSetId, userId, newTeam))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().StartWith("An error occurred updating player team");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularPosition_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task UpdateRegularPosition_RegularSetNotFound_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Regular set not found");
    }

    // Fix logger verification in exception tests
    [Fact]
    public async Task UpdateRegularPosition_ThrowsException_LogsError()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = 1,
                TeamAssignment = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.UpdatePlayerPositionAsync(regularSetId, userId, newPosition))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)), // Note the nullable Exception?
            Times.Once);
    }

    // Similar tests for UpdateRegularTeam
    [Fact]
    public async Task UpdateRegularTeam_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task UpdateRegularTeam_RegularSetNotFound_ReturnsFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Regular set not found");
    }

    [Fact]
    public async Task UpdateRegularTeam_ThrowsException_LogsError()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                TeamAssignment = 1,
                PositionPreference = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.UpdatePlayerTeamAsync(regularSetId, userId, newTeam))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)), // Note the nullable Exception?
            Times.Once);
    }

    [Fact]
    public async Task UpdateRegularPosition_RegularSetWithNoRegulars_ReturnsUserNotFoundFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>(); // Empty regulars list

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
    }

    [Fact]
    public async Task UpdateRegularPosition_NullRegularsCollection_ReturnsUserNotFoundFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = null!; // Null regulars collection

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
    }

    [Fact]
    public async Task UpdateRegularTeam_RegularSetWithNoRegulars_ReturnsUserNotFoundFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>(); // Empty regulars list

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
    }

    [Fact]
    public async Task UpdateRegularTeam_NullRegularsCollection_ReturnsUserNotFoundFailure()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = null!; // Null regulars collection

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
    }

    [Fact]
    public async Task UpdateRegularPosition_PositionNullCheck_HandlesNonMatchingPosition()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newPosition = 2;
        var initialPosition = 1;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId, 
                // Make sure this doesn't match newPosition
                PositionPreference = initialPosition,
                TeamAssignment = 1
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        var updatedSet = CreateTestRegularSet();
        updatedSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = newPosition,
                TeamAssignment = 1
            }
        };

        _mockRegularRepository.Setup(x => x.UpdatePlayerPositionAsync(regularSetId, userId, newPosition))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.UpdateRegularPosition(regularSetId, userId, newPosition);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Regulars.Should().Contain(r => r.UserId == userId && r.PositionPreference == newPosition);
    }

    [Fact]
    public async Task UpdateRegularTeam_TeamNullCheck_HandlesNonMatchingTeam()
    {
        // Arrange
        var regularSetId = 1;
        var userId = "user1";
        var newTeam = 2;
        var initialTeam = 1;

        var user = new AspNetUser { Id = userId, FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = 1,
                // Make sure this doesn't match newTeam
                TeamAssignment = initialTeam
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(regularSetId))
            .ReturnsAsync(regularSet);

        var updatedSet = CreateTestRegularSet();
        updatedSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = userId,
                RegularSetId = regularSetId,
                PositionPreference = 1,
                TeamAssignment = newTeam
            }
        };

        _mockRegularRepository.Setup(x => x.UpdatePlayerTeamAsync(regularSetId, userId, newTeam))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.UpdateRegularTeam(regularSetId, userId, newTeam);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Regulars.Should().Contain(r => r.UserId == userId && r.TeamAssignment == newTeam);
    }

    [Fact]
    public async Task DeleteRegularSet_Success_ReturnsSuccessResult()
    {
        // Arrange
        var regularSetId = 1;
        _mockRegularRepository.Setup(x => x.DeleteRegularSetAsync(regularSetId))
            .ReturnsAsync((true, "Regular set deleted successfully"));

        // Act
        var result = await _regularService.DeleteRegularSet(regularSetId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Regular set deleted successfully");
    }

    [Fact]
    public async Task DeleteRegularSet_InUse_ReturnsFailureResult()
    {
        // Arrange
        var regularSetId = 1;
        _mockRegularRepository.Setup(x => x.DeleteRegularSetAsync(regularSetId))
            .ReturnsAsync((false, "Cannot delete regular set as it is being used by one or more sessions"));

        // Act
        var result = await _regularService.DeleteRegularSet(regularSetId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Cannot delete regular set as it is being used by one or more sessions");
    }

    [Fact]
    public async Task DeleteRegularSet_Exception_ReturnsFailureResult()
    {
        // Arrange
        var regularSetId = 1;
        _mockRegularRepository.Setup(x => x.DeleteRegularSetAsync(regularSetId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _regularService.DeleteRegularSet(regularSetId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Test exception");
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task AddRegular_Success_ReturnsSuccess()
    {
        // Arrange
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var user = new AspNetUser { Id = "test-user", FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        var updatedSet = CreateTestRegularSet();
        updatedSet.Regulars = new List<RegularDetailedResponse>
    {
        new() {
            UserId = request.UserId,
            RegularSetId = request.RegularSetId,
            TeamAssignment = request.TeamAssignment,
            PositionPreference = request.PositionPreference
        }
    };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.AddPlayerAsync(request.RegularSetId, request.UserId,
            request.TeamAssignment, request.PositionPreference))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.AddRegular(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Message.Should().Contain($"{user.FirstName} {user.LastName} added to Regular set");
    }

    [Fact]
    public async Task AddRegular_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _regularService.AddRegular(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task DeleteRegular_Success_ReturnsSuccess()
    {
        // Arrange
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        var user = new AspNetUser { Id = "test-user", FirstName = "Test", LastName = "User" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
    {
        new() {
            UserId = request.UserId,
            RegularSetId = request.RegularSetId,
            TeamAssignment = 1,
            PositionPreference = 2
        }
    };

        var updatedSet = CreateTestRegularSet();
        updatedSet.Regulars = new List<RegularDetailedResponse>();

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.RemovePlayerAsync(request.RegularSetId, request.UserId))
            .ReturnsAsync(updatedSet);

        // Act
        var result = await _regularService.DeleteRegular(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Message.Should().Contain($"{user.FirstName} {user.LastName} removed from Regular set");
    }

    [Fact]
    public async Task DeleteRegular_UserNotInSet_ReturnsFailure()
    {
        // Arrange
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        var user = new AspNetUser { Id = "test-user" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>();

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);

        // Act
        var result = await _regularService.DeleteRegular(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
    }


    // Service tests
    [Fact]
    public async Task AddRegular_RegularSetNotFound_ReturnsFailure()
    {
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var user = new AspNetUser { Id = "test-user" };
        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        var result = await _regularService.AddRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Regular set not found");
    }

    [Fact]
    public async Task AddRegular_UserAlreadyInSet_ReturnsFailure()
    {
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var user = new AspNetUser { Id = "test-user" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = request.UserId,
                RegularSetId = request.RegularSetId,
                TeamAssignment = request.TeamAssignment,
                PositionPreference = request.PositionPreference
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);

        var result = await _regularService.AddRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is already in this Regular set");
    }

    [Fact]
    public async Task AddRegular_AddPlayerFails_ReturnsFailure()
    {
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var user = new AspNetUser { Id = "test-user" };
        var regularSet = CreateTestRegularSet();

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.AddPlayerAsync(It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        var result = await _regularService.AddRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to add player to Regular set");
    }

    [Fact]
    public async Task AddRegular_ThrowsException_ReturnsFailure()
    {
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _regularService.AddRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Test exception");
        _mockLogger.Verify(LogError(), Times.Once);
    }

    [Fact]
    public async Task DeleteRegular_UserNotFound_ReturnsFailure()
    {
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync((AspNetUser) null!);

        var result = await _regularService.DeleteRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task DeleteRegular_RegularSetNotFound_ReturnsFailure()
    {
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        var user = new AspNetUser { Id = "test-user" };
        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        var result = await _regularService.DeleteRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Regular set not found");
    }

    [Fact]
    public async Task DeleteRegular_RemovePlayerFails_ReturnsFailure()
    {
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        var user = new AspNetUser { Id = "test-user" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = new List<RegularDetailedResponse>
        {
            new()
            {
                UserId = request.UserId,
                RegularSetId = request.RegularSetId,
                TeamAssignment = 1,
                PositionPreference = 2
            }
        };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.RemovePlayerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((RegularSetDetailedResponse) null!);

        var result = await _regularService.DeleteRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to remove player from Regular set");
    }

    [Fact]
    public async Task DeleteRegular_ThrowsException_ReturnsFailure()
    {
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _regularService.DeleteRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Test exception");
        _mockLogger.Verify(LogError(), Times.Once);
    }

    private static Expression<Action<ILogger<UserService>>> LogError()
    {
        return x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true));
    }

    [Fact]
    public async Task AddRegular_NullRegularsCollection_DoesNotThrow()
    {
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var user = new AspNetUser { Id = "test-user" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = null!;  // Explicitly set Regulars to null

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);
        _mockRegularRepository.Setup(x => x.AddPlayerAsync(It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(regularSet);

        var result = await _regularService.AddRegular(request);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRegular_NullRegularsCollection_ReturnsFailure()
    {
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user"
        };

        var user = new AspNetUser { Id = "test-user" };
        var regularSet = CreateTestRegularSet();
        regularSet.Regulars = null!;

        _userManager.Setup(x => x.FindByIdAsync(request.UserId))
            .ReturnsAsync(user);
        _mockRegularRepository.Setup(x => x.GetRegularSetAsync(request.RegularSetId))
            .ReturnsAsync(regularSet);

        var result = await _regularService.DeleteRegular(request);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User is not part of this Regular set");
    }
}
