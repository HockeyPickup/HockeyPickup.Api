using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class RegularControllerTests
{
    private readonly Mock<IRegularService> _regularService;
    private readonly Mock<ILogger<AuthController>> _logger;
    private readonly RegularController _controller;

    public RegularControllerTests()
    {
        _regularService = new Mock<IRegularService>();
        _logger = new Mock<ILogger<AuthController>>();
        _controller = new RegularController(_regularService.Object);
    }

    private static RegularSetDetailedResponse CreateTestRegularSet()
    {
        return new RegularSetDetailedResponse
        {
            RegularSetId = 1,
            Description = "Test Regular Set",
            DayOfWeek = 1,
            Archived = false,
            CreateDateTime = DateTime.UtcNow,
            Regulars = new List<RegularDetailedResponse>()
        };
    }

    [Fact]
    public async Task DuplicateRegularSet_Success_ReturnsCreatedResponse()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");

        _regularService.Setup(x => x.DuplicateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DuplicateRegularSet(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task DuplicateRegularSet_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");

        _regularService.Setup(x => x.DuplicateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DuplicateRegularSet(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task UpdateRegularSet_Success_ReturnsOkResponse()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Updated Regular Set",
            DayOfWeek = 2,
            Archived = false
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");
        _regularService.Setup(x => x.UpdateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularSet(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task UpdateRegularSet_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Updated Regular Set",
            DayOfWeek = 2,
            Archived = false
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");
        _regularService.Setup(x => x.UpdateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularSet(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task UpdateRegularSet_InvalidDayOfWeek_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "Updated Regular Set",
            DayOfWeek = 7, // Invalid day
            Archived = false
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Day of week must be between 0 and 6");
        _regularService.Setup(x => x.UpdateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularSet(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Day of week must be between 0 and 6", response.Message);
    }

    [Fact]
    public async Task UpdateRegularSet_EmptyDescription_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "", // Empty description
            DayOfWeek = 2,
            Archived = false
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Description is required");
        _regularService.Setup(x => x.UpdateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularSet(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Description is required", response.Message);
    }

    [Fact]
    public async Task UpdateRegularPosition_Success_ReturnsOkResponse()
    {
        // Arrange
        var request = new UpdateRegularPositionRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            NewPosition = 1
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");
        _regularService.Setup(x => x.UpdateRegularPosition(request.RegularSetId, request.UserId, request.NewPosition))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularPosition(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task UpdateRegularPosition_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularPositionRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            NewPosition = 1
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");
        _regularService.Setup(x => x.UpdateRegularPosition(request.RegularSetId, request.UserId, request.NewPosition))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularPosition(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task UpdateRegularPosition_InvalidPosition_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularPositionRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            NewPosition = 3  // Invalid position
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Invalid position value");
        _regularService.Setup(x => x.UpdateRegularPosition(request.RegularSetId, request.UserId, request.NewPosition))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularPosition(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Invalid position value", response.Message);
    }

    [Fact]
    public async Task UpdateRegularTeam_Success_ReturnsOkResponse()
    {
        // Arrange
        var request = new UpdateRegularTeamRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            NewTeamAssignment = 1
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");
        _regularService.Setup(x => x.UpdateRegularTeam(request.RegularSetId, request.UserId, request.NewTeamAssignment))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularTeam(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task UpdateRegularTeam_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularTeamRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            NewTeamAssignment = 1
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");
        _regularService.Setup(x => x.UpdateRegularTeam(request.RegularSetId, request.UserId, request.NewTeamAssignment))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularTeam(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task UpdateRegularTeam_InvalidTeam_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRegularTeamRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            NewTeamAssignment = 3  // Invalid team assignment
        };
        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Invalid team assignment value");
        _regularService.Setup(x => x.UpdateRegularTeam(request.RegularSetId, request.UserId, request.NewTeamAssignment))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.UpdateRegularTeam(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Invalid team assignment value", response.Message);
    }

    [Fact]
    public async Task DeleteRegularSet_Success_ReturnsOkResponse()
    {
        // Arrange
        var regularSetId = 1;
        var serviceResponse = ServiceResult.CreateSuccess("Success");
        _regularService.Setup(x => x.DeleteRegularSet(regularSetId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteRegularSet(regularSetId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Success", response.Message);
    }

    [Fact]
    public async Task DeleteRegularSet_Failure_ReturnsBadRequest()
    {
        // Arrange
        var regularSetId = 1;
        var serviceResponse = ServiceResult.CreateFailure("Cannot delete: in use");
        _regularService.Setup(x => x.DeleteRegularSet(regularSetId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteRegularSet(regularSetId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Cannot delete: in use", response.Message);
    }

    [Fact]
    public async Task AddRegular_Success_ReturnsCreatedResponse()
    {
        // Arrange
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");

        _regularService.Setup(x => x.AddRegular(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.AddRegular(request);

        // Assert 
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task AddRegular_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new AddRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id",
            TeamAssignment = 1,
            PositionPreference = 2
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");

        _regularService.Setup(x => x.AddRegular(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.AddRegular(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }

    [Fact]
    public async Task DeleteRegular_Success_ReturnsOkResponse()
    {
        // Arrange
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id"
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");

        _regularService.Setup(x => x.DeleteRegular(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteRegular(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task DeleteRegular_Failure_ReturnsBadRequest()
    {
        // Arrange  
        var request = new DeleteRegularRequest
        {
            RegularSetId = 1,
            UserId = "test-user-id"
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");

        _regularService.Setup(x => x.DeleteRegular(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteRegular(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }
}
