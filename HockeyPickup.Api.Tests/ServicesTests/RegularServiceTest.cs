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
            CreateDateTime = DateTime.UtcNow
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
}
